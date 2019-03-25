using System.IO;
using System.Threading.Tasks;
using DyflexisExport.Services.Interfaces;

namespace DyflexisExport.Services
{
	public class LocalDyflexisHtmlScraper : IDyflexisHtmlScraper
	{
		private const string RootFilePath = "D:\\Dyflexis";

		public async Task<string> GetMonthHtml(int year, int month)
		{
			var htmlFilePath = Path.Combine(RootFilePath, $"{year}-{month}.html");
			return await File.ReadAllTextAsync(htmlFilePath);
		}
	}
}
