using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;

namespace SomeApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Started application (Process A)...");
            
            var result = new List<string>();

            // Create separate process
            var anotherProcess = new Process
            {
                StartInfo =
                {
                    FileName = "SomeOtherApp.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };

            // Create 2 anonymous pipes (read and write) for duplex communications (each pipe is one-way)
            using (var pipeRead = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable))
            using (var pipeWrite = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable))
            {
                // Pass to the other process handles to the 2 pipes
                anotherProcess.StartInfo.Arguments = pipeRead.GetClientHandleAsString() + " " + pipeWrite.GetClientHandleAsString();
                anotherProcess.Start();

                Console.WriteLine("Started other process (Process B)...");
                Console.WriteLine();

                pipeRead.DisposeLocalCopyOfClientHandle();
                pipeWrite.DisposeLocalCopyOfClientHandle();

                try
                {
                    using (var sw = new StreamWriter(pipeWrite))
                    {
                        // Send a 'sync message' and wait for the other process to receive it
                        sw.Write("SYNC");
                        pipeWrite.WaitForPipeDrain();

                        Console.WriteLine("Sending message to Process B...");

                        // Send message to the other process
                        sw.Write("Hello from Process A!");
                        sw.Write("END");
                    }

                    // Get message from the other process
                    using (var sr = new StreamReader(pipeRead))
                    {
                        string temp;

                        // Wait for 'sync message' from the other process
                        do
                        {
                            temp = sr.ReadLine();
                        } while (temp == null || !temp.StartsWith("SYNC"));

                        // Read until 'end message' from the other process
                        while ((temp = sr.ReadLine()) != null && !temp.StartsWith("END"))
                        {
                            result.Add(temp);
                        }
                    }
                }
                catch (Exception ex)
                {
                    //TODO Exception handling/logging
                    throw;
                }
                finally
                {
                    anotherProcess.WaitForExit();
                    anotherProcess.Close();
                }

                if (result.Count > 0)
                    Console.WriteLine("Received message from Process B: " + result[0]);

                Console.ReadLine();
            }
        }
    }
}
