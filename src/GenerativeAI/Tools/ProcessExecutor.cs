using Automation.GenerativeAI.Interfaces;
using Automation.GenerativeAI.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Automation.GenerativeAI.Tools
{
    public class ProcessExecutor : FunctionTool
    {
        private ParameterDescriptor parameter;
        private readonly string executablePath;
        private readonly string workingDirectory;

        private ProcessExecutor(string exePath, string workingDir)
        {
            Name = "ProcessExecutor";
            Description = "Executes a configured process with the given arguments, waits until process is complete and returns standard output of the process as text.";
            parameter = new ParameterDescriptor()
            {
                Name = "arguments",
                Type = TypeDescriptor.StringType,
                Description = "Full set of arguments to execute the process."
            };

            if (string.IsNullOrEmpty(workingDir))
            {
                workingDir = Path.GetTempPath();
            }

            executablePath = exePath;
            workingDirectory = workingDir;
        }

        /// <summary>
        /// Creates a process executor tool to execute a given process with appropriate arguments.
        /// </summary>
        /// <param name="exepath">Full path of the executable.</param>
        /// <param name="workingdirectory">Working directory for the process, by default it uses 
        /// user's temp folder as working directory.</param>
        /// <param name="name">Name of the tool</param>
        /// <param name="description">Description of the tool</param>
        public ProcessExecutor(string exepath, string workingdirectory = "", string name = "ProcessExecutor", string description = "") : this(exepath, workingdirectory)
        {
            Name = name; Description = description;
        }

        /// <summary>
        /// Creates an instance of the Process Executor tool.
        /// </summary>
        /// <param name="exePath">Full path of the executable.</param>
        /// <param name="workingDirectory">[optional] Working directory path. By default
        /// it will use the user's temp folder as working directory.</param>
        /// <returns></returns>
        public static ProcessExecutor Create(string exePath, string workingDirectory = "")
        {
            if(string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) 
            {
                throw new ArgumentException($"Not a valid executable path: {exePath}", "exePath");
            }

            if (string.IsNullOrEmpty(workingDirectory))
            {
                workingDirectory = Path.GetTempPath();
            }

            return new ProcessExecutor(exePath, workingDirectory);
        }

        /// <summary>
        /// Executes the process executor tool with the given arguments and returns the
        /// returns standard output of the process as text.
        /// </summary>
        /// <param name="arguments">Full set of arguments to execute the process.</param>
        /// <returns>standard output of the process as text on success or text starting with 
        /// ERROR if there was an error.</returns>
        public string Execute(string arguments)
        {
            try
            {
                if (!File.Exists(executablePath))
                {
                    throw new FileNotFoundException(executablePath);
                }

                var processName = Path.GetFileName(executablePath);
                Logger.WriteLog(LogLevel.Info, File.Exists(executablePath) ? LogOps.Found : LogOps.NotFound, processName);
                Logger.WriteLog(LogLevel.Info, LogOps.Command, processName + " " + arguments);
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = workingDirectory
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string err = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (!string.IsNullOrEmpty(err))
                {
                    Logger.WriteLog(LogLevel.Error, LogOps.Command, err);
                    return $"ERROR: {err}";
                }
                return output;
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogLevel.Error, LogOps.Exception, e.Message);
                Logger.WriteLog(LogLevel.StackTrace, LogOps.Exception, e.StackTrace);
                return $"ERROR: {e.Message}";
            }
        }

        /// <summary>
        /// Executes the process based on the arguments in the execution context
        /// </summary>
        /// <param name="context">ExecutionContext</param>
        /// <param name="output">Process output</param>
        /// <returns>True if successful</returns>
        protected bool Execute(ExecutionContext context, out string output)
        {
            object arguments;
            output = string.Empty;

            if (!context.TryGetValue(parameter.Name, out arguments))
                return false;

            output = Execute(arguments.ToString());
            return !output.StartsWith("ERROR:");
        }

        protected override async Task<Result> ExecuteCoreAsync(ExecutionContext context)
        {
            string output = string.Empty;
            var result = new Result();
            result.success = await Task.Run(() => Execute(context, out output));
            result.output = output;
            return result;
        }

        protected override FunctionDescriptor GetDescriptor()
        {
            var function = new FunctionDescriptor(Name, Description, new List<ParameterDescriptor> { parameter });

            return function;
        }
    }
}
