using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Week02Samples.ContentStream
{
	public class AsyncLoader : IDisposable
	{
		class RESOURCE_REQUEST
		{
			public IDataLoader pDataLoader;
			public IDataProcessor pDataProcessor;
			public bool bLock;
			public bool bCopy;
			public bool bError;
		}

		bool m_bDone;
		int m_NumOustandingResources;
		List<RESOURCE_REQUEST> m_IOQueue = new List<RESOURCE_REQUEST>();
		List<RESOURCE_REQUEST> m_ProcessQueue = new List<RESOURCE_REQUEST>();
		List<RESOURCE_REQUEST> m_RenderThreadQueue = new List<RESOURCE_REQUEST>();
		object m_csIOQueue = new object();
		object m_csProcessQueue = new object();
		object m_csRenderThreadQueue = new object();
		SemaphoreSlim m_hIOQueueSemaphore = new SemaphoreSlim(0);
		SemaphoreSlim m_hProcessQueueSemaphore = new SemaphoreSlim(0);
		Thread m_hIOThread;
		Thread[] m_phProcessThreads;

		//--------------------------------------------------------------------------------------
		// WarmIOCache tells the virtual memory subsystem to prefetch pages for this chunk
		// of memory.  By touching 1 byte in every 4k of data, we can ensure that each page
		// we need is loaded into memory.
		//--------------------------------------------------------------------------------------
		static void WarmIOCache(Stream data)
		{
			// read one byte in every 4k page in order to force all of the pages to load
			data.Position = 0;
			while(data.Position < data.Length)
			{
				data.ReadByte();
				data.Position = Math.Min(data.Position + 1 << 12 - 1, data.Length);
			}
		}

		public AsyncLoader(int NumProcessingThreads)
		{
			// Create the Processing threads
			m_phProcessThreads = new Thread[NumProcessingThreads];
			for(int i = 0; i < NumProcessingThreads; i++)
			{
				m_phProcessThreads[i] = new Thread(ProcessingThreadProc)
				{
					IsBackground = true,
					Name = string.Format("AsyncLoader.ProcessingThread[{0}]", i),
				};
				m_phProcessThreads[i].Start();
			}
			// Create the IO thread
			m_hIOThread = new Thread(FileIOThreadProc)
			{
				IsBackground = true,
				Name = "AsyncLoader.IOThread",
			};
			m_hIOThread.Start();
		}

		public void Dispose()
		{
			m_bDone = true;
			Thread.MemoryBarrier(); // be sure to communicate the new value to all threads!

			m_hIOQueueSemaphore.Release();
			m_hProcessQueueSemaphore.Release();

			Thread.Sleep(100);
			m_hIOQueueSemaphore.Dispose();
			m_hProcessQueueSemaphore.Dispose();

			// LLoyd: this will cause a slow exit.. 
			// and the threads being background & done! they should exit...
			//foreach (var t in m_phProcessThreads)
			//{
			//    t.Interrupt();
			//    t.Abort();
			//}
			//m_hIOThread.Interrupt();
			//m_hIOThread.Abort();
		}

		//--------------------------------------------------------------------------------------
		// Add a work item to the queue of work items
		//--------------------------------------------------------------------------------------
		public void AddWorkItem(IDataLoader pDataLoader, IDataProcessor pDataProcessor)
		{
			if( pDataLoader == null || pDataProcessor == null)
				throw new ArgumentNullException();

			var ResourceRequest = new RESOURCE_REQUEST 
			{
				pDataLoader = pDataLoader,
				pDataProcessor = pDataProcessor,
			};

			// Add the request to the read queue
			lock (m_csIOQueue)
				m_IOQueue.Add(ResourceRequest);

			// TODO: critsec around this?
			Interlocked.Increment(ref m_NumOustandingResources);

			// Signal that we have something to read
			m_hIOQueueSemaphore.Release();
		}

		//--------------------------------------------------------------------------------------
		// Wait for all work in the queues to finish
		//--------------------------------------------------------------------------------------
		public void WaitForAllItems()
		{
			ProcessDeviceWorkItems(int.MaxValue, false);
			for(; ; )
			{
				// Only exit when all resources are loaded
				if( 0 == m_NumOustandingResources )
					return;

				// Service Queues
				ProcessDeviceWorkItems(int.MaxValue, false);
				Thread.Sleep( 100 );
			}
		}

		//--------------------------------------------------------------------------------------
		// FileIOThreadProc
		//
		// This is the one IO threadproc.  This function is responsible for processing read
		// requests made by the application.  There should only be one IO thread per device.  
		// This ensures that the disk is only trying to read one part of the disk at a time.
		//
		// This thread performs double-duty as the copy thread as well.  It manages the copying
		// of resource data from temporary system memory buffer (or memory mapped pointer) into
		// the locked data of the resource.
		//--------------------------------------------------------------------------------------
		void FileIOThreadProc()
		{
			RESOURCE_REQUEST ResourceRequest;
			while (!m_bDone)
			{
				// Wait for a read or create request
				m_hIOQueueSemaphore.Wait();
				if (m_bDone)
					break;

				// Pop a request off of the IOQueue
				lock (m_csIOQueue)
				{
					ResourceRequest = m_IOQueue[0];
					m_IOQueue.RemoveAt(0);
				}

				// Handle a read request
				if (!ResourceRequest.bCopy)
				{
					if (!ResourceRequest.bError)
					{
						// Load the data
						try
						{
							ResourceRequest.pDataLoader.Load();
						}
						catch (Exception ex)
						{
							Console.WriteLine("PROBLEM " + ex.Message + "\r\n" + ex.StackTrace);
							ResourceRequest.bError = true;
						}
					}

					// Add it to the ProcessQueue
					lock (m_csProcessQueue)
						m_ProcessQueue.Add(ResourceRequest);

					// Let the process thread know it's got work to do
					m_hProcessQueueSemaphore.Release();
				}

					// Handle a copy request
				else
				{
					if (!ResourceRequest.bError)
					{
						// Create the data
						try
						{
							ResourceRequest.bError = !ResourceRequest.pDataProcessor.CopyToResource();
						}
						catch (Exception ex)
						{
							Console.WriteLine("PROBLEM " + ex.Message + "\r\n" + ex.StackTrace);
							ResourceRequest.bError = true;
						}
					}

					// send an unlock request
					ResourceRequest.bLock = false;
					lock (m_csRenderThreadQueue)
						m_RenderThreadQueue.Add(ResourceRequest);
				}
			}
		}

		//--------------------------------------------------------------------------------------
		// ProcessingThreadProc
		// 
		// This is the threadproc for the processing thread.  There are multiple processing
		// threads.  The job of the processing thread is to uncompress, unpack, or otherwise
		// manipulate the data loaded by the loading thread in order to get it ready for the
		// ProcessDeviceWorkItems function in the graphics thread to lock or unlock the resource.
		//--------------------------------------------------------------------------------------
		void ProcessingThreadProc()
		{
			while( !m_bDone )
			{
				// Acquire ProcessQueueSemaphore
				m_hProcessQueueSemaphore.Wait();
				if( m_bDone )
					break;

				// Pop a request off of the ProcessQueue
				RESOURCE_REQUEST ResourceRequest;
				lock(m_csProcessQueue)
				{
					ResourceRequest = m_ProcessQueue[0];
					m_ProcessQueue.RemoveAt(0);
				}

				// Decompress the data
				if( !ResourceRequest.bError )
				{
					try
					{
						var pData = ResourceRequest.pDataLoader.Decompress();
						ResourceRequest.pDataProcessor.Process(pData);
					}
					catch (Exception ex)
					{
						Console.WriteLine("PROBLEM " + ex.Message + "\r\n" + ex.StackTrace);
						ResourceRequest.bError = false;
					}
				}

				// Add it to the RenderThreadQueue
				ResourceRequest.bLock = true;
				lock (m_csRenderThreadQueue)
					m_RenderThreadQueue.Add(ResourceRequest);
			}
		}

		//--------------------------------------------------------------------------------------
		// ProcessDeviceWorkItems is called by the graphics thread.  Depending on the request
		// it either Locks or Unlocks a resource (or calls UpdateSubresource for D3D10).  One of
		// of the arguments is the number of resources to service.  This ensure that no matter
		// how many items are in the queue, the graphics thread doesn't stall trying to process
		// all of them.
		//--------------------------------------------------------------------------------------
		public void ProcessDeviceWorkItems(int CurrentNumResourcesToService, bool bRetryLoads = false)
		{
			int numJobs;
			lock (m_csRenderThreadQueue)
				numJobs = m_RenderThreadQueue.Count;

			for( int i = 0; i < numJobs && i < CurrentNumResourcesToService; i++ )
			{
				RESOURCE_REQUEST ResourceRequest;
				lock (m_csRenderThreadQueue)
				{
					ResourceRequest = m_RenderThreadQueue[0];
					m_RenderThreadQueue.RemoveAt(0);
				}

				if( ResourceRequest.bLock )
				{
					if( !ResourceRequest.bError )
					{
						bool succeeded;
						try
						{
							succeeded = ResourceRequest.pDataProcessor.LockDeviceObject();
						}
						catch (Exception ex)
						{
							Console.WriteLine("PROBLEM " + ex.Message + "\r\n" + ex.StackTrace);
							succeeded = false;
						}
						if (!succeeded && bRetryLoads)
						{
							// add it back to the list
							lock (m_csRenderThreadQueue)
								m_RenderThreadQueue.Add(ResourceRequest);

							// move on to the next guy
							continue;
						}
						else if (!succeeded)
						{
							ResourceRequest.bError = true;
						}
					}

					ResourceRequest.bCopy = true;
					lock (m_csIOQueue)
						m_IOQueue.Add(ResourceRequest);

					// Signal that we have something to copy
					m_hIOQueueSemaphore.Release();
				}
				else
				{
					if( !ResourceRequest.bError )
						ResourceRequest.pDataProcessor.UnLockDeviceObject();

					ResourceRequest.pDataLoader.Dispose();
					ResourceRequest.pDataProcessor.Dispose();

					// Decrement num oustanding resources
					Interlocked.Decrement(ref m_NumOustandingResources);
				}
			}
		}
	
	}
}
