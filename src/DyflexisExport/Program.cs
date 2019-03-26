using System;
using System.Diagnostics;
using Autofac;
using DyflexisExport.Providers;
using DyflexisExport.Services;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace DyflexisExport
{
	public class Program
	{
		private static void SetUpNlog()
		{
			var loggingConfiguration = new LoggingConfiguration();

			const string nlogFormat = @"[${date:format=HH\:mm\:ss}] [${level}] ${message} ${exception:format=toString}";

			var coloredConsoleTarget = new ColoredConsoleTarget
			{
				Layout = nlogFormat
			};

			loggingConfiguration.AddTarget("console", coloredConsoleTarget);
			loggingConfiguration.AddRule(LogLevel.Trace, LogLevel.Fatal, coloredConsoleTarget);

			var fileTarget = new FileTarget
			{
				FileName = "logs/${shortdate}.log",
				ArchiveAboveSize = 1024 * 1024 * 5, // 5 MB
				Layout = nlogFormat
			};

			loggingConfiguration.AddTarget("file", fileTarget);
			loggingConfiguration.AddRule(LogLevel.Trace, LogLevel.Fatal, fileTarget);

			LogManager.Configuration = loggingConfiguration;
		}

		public static void Main(string[] args)
		{
			SetUpNlog();

			var builder = new ContainerBuilder();

			builder.RegisterType<ExportService>()
				.AsImplementedInterfaces();

			builder.RegisterType<AuthenticationService>()
				.SingleInstance()
				.AsSelf();

			builder.RegisterType<CalendarServiceProvider>()
				.AsSelf();

			builder.RegisterType<UserCredentialProvider>()
				.SingleInstance()
				.AsSelf();

			builder.RegisterType<ConsoleControlService>()
				.SingleInstance()
				.AsSelf();

			builder.RegisterType<DyflexisEventsService>()
				.SingleInstance()
				.AsSelf();

			builder.RegisterType<CustomDataStoreService>()
				.SingleInstance()
				.AsImplementedInterfaces();

			builder.RegisterType<DyflexisHtmlScraper>()
				.SingleInstance()
				.AsImplementedInterfaces();

#if DEBUG
			builder.RegisterType<LocalDyflexisHtmlScraper>()
				.SingleInstance()
				.AsImplementedInterfaces();
#endif
			
			var container = builder.Build();

			var consoleControlService = container.Resolve<ConsoleControlService>();
			consoleControlService.ShutDownRequested += () => Process.GetCurrentProcess().Kill();
			
			if (SettingsService.Settings.IsRunningSetup)
				Console.ReadKey();

			container.Dispose();
		}
	}
}