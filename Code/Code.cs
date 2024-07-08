using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class FolderSynchronizer
{
    private static string sourcePath;
    private static string replicaPath;
    private static int syncInterval;
    private static string logFilePath;
    private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    private static object e;

    static void Main(string[] args)
    {
        if (args.Length != 4)
        {
            Console.WriteLine("Usage: FolderSynchronizer <sourcePath> <replicaPath> <syncIntervalSeconds> <logFilePath>");
            return;
        }

        sourcePath = args[0];
        replicaPath = args[1];
        syncInterval = int.Parse(args[2]);
        logFilePath = args[3];

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        Task.Run(() => SynchronizePeriodically(cancellationTokenSource.Token)).Wait();

    }

    private static async Task SynchronizePeriodically(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            SynchronizeFolders();
            await Task.Delay(syncInterval * 1000, token);
        }
    }

    private static void SynchronizeFolders()
    {
        try
        {
            if (!Directory.Exists(sourcePath))
            {
                Log($"Source directory does not exist: {sourcePath}");
                return;
            }

            if (!Directory.Exists(replicaPath))
            {
                Directory.CreateDirectory(replicaPath);
                Log($"Created replica directory: {replicaPath}");
            }

            SyncDirectories(new DirectoryInfo(sourcePath), new DirectoryInfo(replicaPath));
        }
        catch (Exception ex)
        {
            Log($"Error during synchronization: {ex.Message}");
        }
    }

    private static void SyncDirectories(DirectoryInfo sourceDir, DirectoryInfo replicaDir)
    {

        foreach (var sourceFile in sourceDir.GetFiles())
        {
            string replicaFilePath = Path.Combine(replicaDir.FullName, sourceFile.Name);
            if (!File.Exists(replicaFilePath) || !FilesAreEqual(sourceFile, new FileInfo(replicaFilePath)))
            {
                sourceFile.CopyTo(replicaFilePath, true);
                Log($"Copied file: {sourceFile.FullName} to {replicaFilePath}");
            }
        }


        foreach (var replicaFile in replicaDir.GetFiles())
        {
            if (!File.Exists(Path.Combine(sourceDir.FullName, replicaFile.Name)))
            {
                replicaFile.Delete();
                Log($"Deleted file: {replicaFile.FullName}");
            }
        }


        foreach (var sourceSubDir in sourceDir.GetDirectories())
        {
            string replicaSubDirPath = Path.Combine(replicaDir.FullName, sourceSubDir.Name);
            if (!Directory.Exists(replicaSubDirPath))
            {
                Directory.CreateDirectory(replicaSubDirPath);
                Log($"Created directory: {replicaSubDirPath}");
            }

            SyncDirectories(sourceSubDir, new DirectoryInfo(replicaSubDirPath));
        }


        foreach (var replicaSubDir in replicaDir.GetDirectories())
        {
            if (!Directory.Exists(Path.Combine(sourceDir.FullName, replicaSubDir.Name)))
            {
                replicaSubDir.Delete(true);
                Log($"Deleted directory: {replicaSubDir.FullName}");
            }
        }
    }

    private static bool FilesAreEqual(FileInfo file1, FileInfo file2)
    {
        using (var md5 = MD5.Create())
        {
            using (var stream1 = file1.OpenRead())
            using (var stream2 = file2.OpenRead())
            {
                return md5.ComputeHash(stream1).SequenceEqual(md5.ComputeHash(stream2));
            }
        }
    }

    private static void Log(string message)
    {
        string logMessage = $"{DateTime.Now}: {message}";
        Console.WriteLine(logMessage);
        File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
    }
}