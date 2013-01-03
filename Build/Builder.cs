using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Build
{
	class Builder
	{
		List<Project> projectsToBuild, allProjects;
		List<string> builtAssemblies, failedAssemblies;

		internal Builder()
		{
			DeleteExistingLog();
		}

		internal void Build(string projectPath, bool runTests = false)
		{
			var targetProjectFiles = GetProjectFiles(projectPath);

			builtAssemblies = new List<string>();
			failedAssemblies = new List<string>();
			projectsToBuild = targetProjectFiles.Select(projectFile => new Project(projectFile)).ToList<Project>();
			allProjects = projectsToBuild.ToList();

			while (projectsToBuild.Count > 0)
			{
				try
				{
					var project = GetNextProjectToBuild();
					string buildResult = BuildAndReturnStdOut(project);

					string assemblyName = project.AssemblyName;
					if (DetermineSuccessFromBuildStdOut(buildResult))
					{
						builtAssemblies.Add(assemblyName);
					}
					else
					{
						Console.WriteLine("*** Build failed ***");
						failedAssemblies.Add(assemblyName);
						Log(buildResult);
					}
					projectsToBuild.Remove(project);
				}
				catch (InvalidOperationException)
				{
					failedAssemblies.AddRange(projectsToBuild.Select(project => project.AssemblyName));
					projectsToBuild.Clear();
				}
			}

			DisplayOutcome(builtAssemblies, failedAssemblies);

			if (runTests)
			{
				RunTests();
			}
#if DEBUG
			Console.WriteLine();
			Console.Write("Press any key to exit...");
			Console.ReadLine();
#endif
		}

		string BuildAndReturnStdOut(Project project)
		{
			Console.WriteLine("Building project {0}", project.AssemblyName);
			return RunAndReturnStdOut(MSBuildPath, project.ProjectFilePath);
		}

		string RunAndReturnStdOut(string path, string args)
		{
			var process = new Process();
			process.StartInfo = new ProcessStartInfo(path, args);
			process.StartInfo.CreateNoWindow = false;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = true;
			process.Start();
			process.WaitForExit(10000); // hack to get around issue with process not returning exit code correctly sometimes
			return process.StandardOutput.ReadToEnd();
		}

		bool DetermineSuccessFromBuildStdOut(string stdout)
		{
			string re1 = "(\\d+)";	// Integer Number 1
			string re2 = ".*?";	// Non-greedy match on filler
			string re3 = "(Error)";	// Any Single Character 1
			Regex regex = new Regex(re1 + re2 + re3);

			Match match = regex.Match(stdout);
			if (match.Success)
			{
				string errorsAsString = match.Groups[1].ToString();
				int errors;
				if (int.TryParse(errorsAsString, out errors))
				{
					if (errors > 0)
					{
						Console.WriteLine("There were {0} build errors, see log for details", errors);
						Console.WriteLine(stdout);
						Console.Write("Press Enter to continue.");
						Console.ReadLine();

						return false;
					}
					else
					{
						return true;
					}
				}
			}
			Log("Couldn't determine build success: " + stdout);
			return false;
		}

		static void DeleteExistingLog()
		{
			if (File.Exists(logFilePath))
			{
				File.Delete(logFilePath);
			}
		}

		const string logFilePath = "buildFailure.log";

		static void Log(string contents)
		{
			var logDetails = string.Format(@"**************************************{0}Build.exe Failure Log{0}{1}{0}**************************************{0}{2}{0}**************************************{0}",
				Environment.NewLine, DateTime.Now.ToString(), contents);
			File.AppendAllText(logFilePath, logDetails);
		}

		static string MSBuildPath
		{
			get
			{
				if (msBuildPath == null)
				{
					string[] dotNetFrameworkDirectories = Directory.GetDirectories(@"C:\Windows\Microsoft.NET\Framework");
					string dotNet4Directory = dotNetFrameworkDirectories.FirstOrDefault(path => path.Contains("v4.0"));
					string msbuildPath = Path.Combine(dotNet4Directory, "MSBuild.exe");
					if (File.Exists(msbuildPath))
					{
						msBuildPath = msbuildPath;
					}
					else
					{
						throw new FileNotFoundException("MSBuild.exe not found", msbuildPath);
					}
				}
				return msBuildPath;
			}
		}
		static string msBuildPath;

		static string MSTestPath
		{
			get { return @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\IDE\MSTest.exe"; }
		}

		static string[] GetProjectFiles(string projectPath)
		{
			string[] projectFiles = Directory.GetFiles(projectPath, "*.csproj", SearchOption.AllDirectories);
			if (projectFiles.Length > 0)
			{
				return projectFiles;
			}
			else
			{
				throw new FileNotFoundException("No project files found", projectPath + "\\*.csproj");
			}
		}

		Project GetNextProjectToBuild()
		{
			return projectsToBuild
				.First(project => project.References
					.All(reference => builtAssemblies.Contains(reference) || !allProjects.Any(p => p.AssemblyName == reference)));
		}

		static void DisplayOutcome(List<string> builtProjects, List<string> failedProjects)
		{
			if (builtProjects.Count == 0 && failedProjects.Count == 0)
			{
				Console.WriteLine("No projects targeted to build.");
			}
			else
			{
				Console.WriteLine("Build completed.");
				if (builtProjects.Count > 0 && failedProjects.Count > 0)
				{
					Console.WriteLine();
					Console.WriteLine("The following projects were successfully built:");
					builtProjects.ForEach(project => Console.WriteLine("\t{0}", project));
				}

				if (failedProjects.Count > 0)
				{
					Console.WriteLine();
					Console.WriteLine("The following projects could not be built:");
					failedProjects.ForEach(project => Console.WriteLine("\t{0}", project));
				}
			}
		}

		internal void RunTests()
		{
			Console.WriteLine("\r\nRunning tests...\r\n");
			var projectOutputPaths = string.Join(" ", allProjects.Select(p => string.Format("/testcontainer:\"{0}\"", p.OutputAssemblyPath)));
			var process = Process.Start(new ProcessStartInfo
			{
				FileName = MSTestPath,
				Arguments = projectOutputPaths,
				UseShellExecute = false,
			});
			process.WaitForExit();
		}
	}
}
