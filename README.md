# C# Anonymous Pipes for Interprocess Communication
There are times when you may need to communicate between multiple processes, perhaps to send messages or share data. This repo provides an example of anonymous pipes (bidirectional) for interprocess communication (IPC). A use case that I had involved a 64-bit application that I was working on which had a need to use a 32-bit DLL. The 32-bit DLL had to run in a separate process, so interprocess communication (IPC) was needed to allow the two processes to work together.

The .NET Framework provides a number of ways to perform IPC. In this post we’ll explore one such method: anonymous pipes. Anonymous pipes don’t provide as much functionality as some other methods (i.e. named pipes), and you can’t use it for communication over a network. But when you need to communicate between processes on the same computer, anonymous pipes are an excellent choice as they require less overhead.

In the “parent” (or “server”) process, we begin by creating a separate process.
```C#
            var anotherProcess = new Process
            {
                StartInfo =
                {
                    FileName = "SomeOtherApp.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };
```
The FileName property should equal the executable for the second process, and can include a full or relative path. In the code above I am assuming that “SomeOtherApp.exe” is going to be located in the same directory.

We then use the [AnonymousPipeServerStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.anonymouspipeserverstream) class to expose a stream around an anonymous pipe. Anonymous pipes are unidirectional, so if you need bidirectional communication, you will need two pipes. When you instantiate an AnonymousPipeServerStream, you can specify the [pipe direction (in or out)](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.pipedirection).
```C#
using (var pipeRead = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable))
using (var pipeWrite = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable))
{
}
```
Once the anonymous pipes have been instantiated, handles to the pipes can be passed to the second process, and then that process can be started.
```C#
anotherProcess.StartInfo.Arguments = pipeRead.GetClientHandleAsString() + " " + 
    pipeWrite.GetClientHandleAsString();
anotherProcess.Start();
```
We then need to release the local handles that were created with the above GetClientHandleAsString calls:
```C#
pipeRead.DisposeLocalCopyOfClientHandle();
pipeWrite.DisposeLocalCopyOfClientHandle();
```
If DisposeLocalCopyOfClientHandle is not called, the anonymous pipe will not receive notice when the child/client process disposes its pipe stream.

Now that the pipes have been established, we can read/write values with the other process.
```C#
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
```
In the code above I am only passing strings between the two processes, so I am using StreamWriter/StreamReader. If you had other needs, you could use something else, such as BinaryWriter/BinaryReader.

Once we’re done, we call WaitForExit and Close to end the second process that we created.
```C#
anotherProcess.WaitForExit();
anotherProcess.Close();
```
The code for the “child” (or “client”) process is similar, with a few notable exceptions. We first have to get the anonymous pipe handles that we previously passed to the second process.
```C#
// Get read and write pipe handles
// Note: Roles are now reversed from how the other process is passing the handles in
string pipeWriteHandle = args[0];
string pipeReadHandle = args[1];
```
Please note that we passed in the parent process’s read and then write handles, but to the second (“client”) process, the roles are reversed. The pipe that the parent process is reading from is the one that the child will be writing to, and the pipe that the parent process is writing to is the one that the child will be reading from.

Also, in the parent process we used the AnonymousPipeServerStream class. In the client process we need to use the AnonymousPipeClientStream class instead to expose a stream around an anonymous pipe. Once again, since the pipes are unidirectional, if you need bidirectional communication you will need two pipes.
```C#
using (var pipeRead = new AnonymousPipeClientStream(PipeDirection.In, pipeReadHandle))
using (var pipeWrite = new AnonymousPipeClientStream(PipeDirection.Out, pipeWriteHandle))
{
}
```
In this repo, the code for the parent (server) process can be found [here](https://github.com/jingeno/csharp-anonymous-pipes/blob/master/AnonymousPipeExample/SomeApp/Program.cs). The code for the child (client) process can be found [here](https://github.com/jingeno/csharp-anonymous-pipes/blob/master/AnonymousPipeExample/SomeOtherApp/Program.cs).
