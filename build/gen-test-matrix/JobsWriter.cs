using GenTestMatrix.Models;
using System.Text;
using System.Text.Json;

namespace GenTestMatrix
{
    internal sealed class JobsWriter : IAsyncDisposable
    {
        private readonly List<Job> jobs = new();
        private readonly FileStream outputStream;
        private readonly StreamWriter writer;
        private readonly string[] outputNames;
        private int nextOutput;

        public JobsWriter(FileStream outputStream, string[] outputNames)
        {
            this.outputStream = outputStream;
            writer = new(outputStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            this.outputNames = outputNames;
        }

        public ValueTask AddJob(Job job)
        {
            jobs.Add(job);

            if (jobs.Count == Constants.MaxJobCountPerMatrix)
            {
                return FlushAsync();
            }
            return default;
        }

        public async ValueTask FlushAsync()
        {
            if (nextOutput >= outputNames.Length)
            {
                throw new InvalidOperationException($"Not enough output names were specified (need at least {nextOutput})");
            }

            var outName = outputNames[nextOutput++];
            await writer.WriteAsync(outName);
            await writer.WriteAsync('=');
            await writer.FlushAsync();

            await JsonSerializer.SerializeAsync(outputStream, new MatrixResult { Jobs = jobs }, JsonCtx.Default.MatrixResult);
            await writer.WriteLineAsync();
            jobs.Clear();
        }

        public async ValueTask DisposeAsync()
        {
            await FlushAsync();
            await writer.DisposeAsync();
            await outputStream.DisposeAsync();
        }
    }
}
