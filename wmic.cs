C# Implementation (wmic.cs)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WmicWrapper
{
    class Program
    {
        private static readonly Dictionary<string, string> AliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "bios", "Win32_BIOS" },
            { "computersystem", "Win32_ComputerSystem" },
            { "diskdrive", "Win32_DiskDrive" },
            { "logicaldisk", "Win32_LogicalDisk" },
            { "os", "Win32_OperatingSystem" },
            { "process", "Win32_Process" },
            { "service", "Win32_Service" },
            { "nicconfig", "Win32_NetworkAdapterConfiguration" },
            { "baseboard", "Win32_BaseBoard" },
            { "memorychip", "Win32_PhysicalMemory" }
        };

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("WMIC Compatibility Wrapper (C# CLI)");
                Console.WriteLine("Usage: wmic <alias> [where <condition>] <get/list> [properties]");
                return;
            }

            var cleanArgs = args.Where(arg => !arg.StartsWith("/")).ToList();
            if (cleanArgs.Count == 0)
            {
                Console.Error.WriteLine("ERROR: No operation specified.");
                Environment.Exit(1);
            }

            string aliasKey = cleanArgs[0];
            if (!AliasMap.TryGetValue(aliasKey, out string wmiClass))
            {
                Console.Error.WriteLine($"ERROR: Alias '{aliasKey}' not supported in compatibility wrapper.");
                Environment.Exit(1);
            }

            string filterStr = "";
            int whereIndex = cleanArgs.FindIndex(x => x.Equals("where", StringComparison.OrdinalIgnoreCase));
            if (whereIndex != -1 && cleanArgs.Count > whereIndex + 1)
            {
                filterStr = cleanArgs[whereIndex + 1];
                cleanArgs.RemoveAt(whereIndex + 1);
                cleanArgs.RemoveAt(whereIndex);
            }

            string verb = "get";
            List<string> properties = new List<string>();

            if (cleanArgs.Count > 1)
            {
                verb = cleanArgs[1].ToLower();
                if (cleanArgs.Count > 2)
                {
                    string propsRaw = string.Join(" ", cleanArgs.Skip(2));
                    properties = propsRaw.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                }
            }

            string psCommand = TranslateToPowerShell(wmiClass, verb, filterStr, properties);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{psCommand}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(output)) Console.Write(output);
                if (!string.IsNullOrEmpty(error)) Console.Error.Write(error);
                Environment.Exit(process.ExitCode);
            }
        }

        private static string TranslateToPowerShell(string wmiClass, string verb, string filterStr, List<string> properties)
        {
            if (verb == "get")
            {
                string propStr = properties.Count > 0 ? string.Join(", ", properties.Select(p => $"'{p}'")) : "*";
                string filterParam = !string.IsNullOrEmpty(filterStr) ? $"-Filter \"{filterStr}\"" : "";
                return $"Get-CimInstance -ClassName {wmiClass} {filterParam} | Select-Object {propStr} | Format-Table -AutoSize";
            }
            else if (verb == "list")
            {
                string filterParam = !string.IsNullOrEmpty(filterStr) ? $"-Filter \"{filterStr}\"" : "";
                return $"Get-CimInstance -ClassName {wmiClass} {filterParam} | Format-List";
            }
            else
            {
                return $"Get-CimInstance -ClassName {wmiClass} | Format-Table";
            }
        }
    }
}
