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
		internal Builder()
		{
		}

		internal void Build(string projectPath)
		{
			var msBuildPath = GetMSBuildPath();
			var targetProjectFiles = GetProjectFiles(projectPath);
			var projectReferences = GetProjectReferences(targetProjectFiles);

			var projectsToBuild = targetProjectFiles.Select(project => GetAssemblyName(project)).ToList<string>();
			var builtProjects = new List<string>();
			var failedProjects = new List<string>();

			while (projectsToBuild.Count > 0 && failedProjects.Count < targetProjectFiles.Length - builtProjects.Count)
			{
				try
				{
					var projectToBuild = GetNextProjectToBuild(projectReferences, builtProjects);
					string buildResult = BuildAssemblyAndReturnStdOut(msBuildPath, projectToBuild);

					string assemblyName = GetAssemblyName(projectToBuild);
					if (DetermineSuccessFromBuildStdOut(buildResult))
					{
						builtProjects.Add(assemblyName);
					}
					else
					{
						failedProjects.Add(assemblyName);
					}
					projectsToBuild.Remove(assemblyName);
					projectReferences.Remove(projectToBuild);
				}
				catch (InvalidOperationException)
				{
					failedProjects.AddRange(projectsToBuild);
					projectsToBuild.Clear();
				}
			}

			DisplayOutcome(builtProjects, failedProjects);
#if DEBUG
			Console.ReadLine();
#endif
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
						Console.WriteLine("There were {0} build errors", errors);
						Console.WriteLine(stdout);
						Console.Write("Press Enter to continue.");
						Console.ReadLine();
					}
					else
					{
						return true;
					}
				}
			}
			return false;
		}

		string GetMSBuildPath()
		{
			string[] dotNetFrameworkDirectories = Directory.GetDirectories(@"C:\Windows\Microsoft.NET\Framework");
			string dotNet4Directory = dotNetFrameworkDirectories.FirstOrDefault(path => path.Contains("v4.0"));
			string msbuildPath = Path.Combine(dotNet4Directory, "MSBuild.exe");
			if (File.Exists(msbuildPath))
			{
				return msbuildPath;
			}
			else
			{
				throw new FileNotFoundException("MSBuild.exe not found", msbuildPath);
			}
		}

		string[] GetProjectFiles(string projectPath)
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

		Dictionary<string, string[]> GetProjectReferences(string[] projectFiles)
		{
			var projectReferences = new Dictionary<string, string[]>();
			foreach (string projectFile in projectFiles)
			{
				projectReferences.Add(projectFile, GetReferencesForProject(projectFile).ToArray());
			}

			return projectReferences;
		}

		IEnumerable<string> GetReferencesForProject(string projectFile)
		{
			string[] fileContents = File.ReadAllLines(projectFile);
			foreach (string line in fileContents)
			{
				int indexOfReferencePrefix = line.IndexOf(assemblyReferencePrefix, StringComparison.OrdinalIgnoreCase);
				if (indexOfReferencePrefix >= 0)
				{
					int indexOfReference = indexOfReferencePrefix + assemblyReferencePrefix.Length;
					string lineStartingAtReference = line.Substring(indexOfReference);
					string reference = lineStartingAtReference.Split(',', '\"')[0];
					if (!IsSystemReference(reference))
					{
						yield return reference;
					}
				}
			}
		}

		const string assemblyReferencePrefix = "Reference Include=\"";

		bool IsSystemReference(string reference)
		{
			return systemAssemblyPrefixes.Any(systemRef => reference.StartsWith(systemRef, StringComparison.OrdinalIgnoreCase));
		}

		readonly string[] systemAssemblyPrefixes = new string[] { "System", "Microsoft", "Windows", "Presentation", "nunit" };

		string BuildAssemblyAndReturnStdOut(string msBuildPath, string projectFile)
		{
			string assemblyName = GetAssemblyName(projectFile);
			Console.WriteLine("Building project {0}", assemblyName);
			
			Process process = new Process();
			process.StartInfo = new ProcessStartInfo(msBuildPath, projectFile);
			process.StartInfo.CreateNoWindow = false;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = true;
			process.Start();
			process.WaitForExit();
			return process.StandardOutput.ReadToEnd();
		}

		string GetAssemblyName(string projectFile)
		{
			string[] projectFileContents = File.ReadAllLines(projectFile);
			string assemblyNameIdentifier = "<AssemblyName>";
			string assemblyNameLine = projectFileContents.First(line => line.Contains(assemblyNameIdentifier));
			int indexOfAssemblyName = assemblyNameLine.IndexOf(assemblyNameIdentifier) + assemblyNameIdentifier.Length;
			int indexOfEndAssembly = assemblyNameLine.IndexOf('<', indexOfAssemblyName);

			return assemblyNameLine.Substring(indexOfAssemblyName, indexOfEndAssembly - indexOfAssemblyName);
		}

		string GetNextProjectToBuild(Dictionary<string, string[]> references, List<string> builtProjects)
		{
			var projectReferenceSet = references.First(referenceSet => referenceSet.Value.Length == 0
				|| referenceSet.Value.All(reference => builtProjects.Contains(reference)));
			return projectReferenceSet.Key;
		}

		void DisplayOutcome(List<string> builtProjects, List<string> failedProjects)
		{
			if (builtProjects.Count > 0)
			{
				Console.WriteLine("Process completed.");
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

			if (builtProjects.Count == 0 && failedProjects.Count == 0)
			{
				Console.WriteLine("No projects targeted to build.");
			}
		}
	}
}
