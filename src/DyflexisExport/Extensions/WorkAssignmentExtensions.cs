using DyflexisExport.Models;
using Google.Apis.Calendar.v3.Data;

namespace DyflexisExport.Extensions
{
	public static class WorkAssignmentExtensions
	{
		public const string EventDescriptionPrefix = "WorkAssignment:";

		public static Event ToEvent(this WorkAssignment workAssignment)
		{
			return new Event
			{
				Summary = workAssignment.Placement,
				Start = new EventDateTime { DateTime = workAssignment.Start.DateTime, TimeZone = "Europe/Amsterdam" },
				End = new EventDateTime { DateTime = workAssignment.End.DateTime, TimeZone = "Europe/Amsterdam" },
				Description = workAssignment.EventDescription()
			};
		}

		public static string EventDescription(this WorkAssignment workAssignment) =>
			EventDescriptionPrefix + workAssignment.Id;

		public static string ToLogInfo(this WorkAssignment workAssignment) =>
			$"({workAssignment.Id}) [{workAssignment.Start.DateTime:d}] {workAssignment.Start.DateTime:t} -> {workAssignment.End.DateTime:t} - {workAssignment.Placement}";
	}
}
