namespace DyflexisExport.Services
{
	public class ConsoleControlService
	{
		public delegate void ShutDownHandler();

		public event ShutDownHandler ShutDownRequested;

		public void ShutDown()
		{
			ShutDownRequested?.Invoke();
		}
	}
}
