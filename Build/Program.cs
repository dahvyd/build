using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace Build
{
	class Program
	{
		static void Main(string[] args)
		{
			string filePath = args.Length > 0 ? args[0] : GetFilePathFromConsole();
			if (Directory.Exists(filePath))
			{
				var builder = new Builder();
				builder.Build(filePath, args.Contains("-t"));
			}
			else
			{
				Console.WriteLine("Couldn't find build path {0}, exiting.", filePath);
			}
		}

		static string GetFilePathFromConsole()
		{
			Console.Write("Enter a build target file path: ");
			return Console.ReadLine();
		}
	}
}
