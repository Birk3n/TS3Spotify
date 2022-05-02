
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TSLib.Audio;
using TS3AudioBot.Audio;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using TS3AudioBot.Config;
using System.Collections.Generic;

public class SpotifyStreamAudioProducer : IPlayerSource, IDisposable
{
    public event EventHandler OnSongEnd;
    public event EventHandler<SongInfoChanged> OnSongUpdated;
    public TimeSpan? Length { get; }
    public TimeSpan? Position { get; set; }

    public Task Seek(TimeSpan position) { return Task.CompletedTask; }

    private readonly Stream stream;

    public bool Closed = true;
    private bool useResampledStream = true;

    private Thread mainReadLoop;
    private MemoryStream resampledStream;

    private Queue<byte[]> pendingSeconds = new Queue<byte[]>();
    private AutoResetEvent wait;

    private static Resampler resampler;

    private readonly Player player;
    private bool wasFired = true;
    public SpotifyStreamAudioProducer(Stream stream, Player player, ConfRoot config)
    {
        this.stream = stream;
        this.player = player;

        wait = new AutoResetEvent(false);

        resampledStream = new MemoryStream();

        if (resampler == null)
        {
            /*Assembly.LoadFrom(Path.GetFullPath(Path.Combine(config.Plugins.Path.Value, "lib", "NAudio.Core.dll")));
            Assembly.LoadFrom(Path.GetFullPath(Path.Combine(config.Plugins.Path.Value, "lib", "NAudio.WinMM.dll")));*/
            Assembly.LoadFrom(Path.GetFullPath(Path.Combine(config.Plugins.Path.Value, "lib", "NAudio.dll")));

            resampler = new Resampler();
        }
    }
    public int Read(byte[] buffer, int offset, int length, out Meta meta)
    {
        meta = null;
        if (Closed) return 0;
        var streamToUse = getCurrentStream();

        int read = streamToUse.Read(buffer, offset, length);

        if (streamToUse.Position == streamToUse.Length && pendingSeconds.Count > 0)
        {
            //Dequeue one, read it, continue in Readloop
            var tbuffer = pendingSeconds.Dequeue();

            streamToUse.Position = 0;
            streamToUse.Write(tbuffer, 0, tbuffer.Length);
            streamToUse.Position = 0;


            read = streamToUse.Read(buffer, offset, length);

            wait.Set();
        }

        if (read == 0 && !Closed)
        {
            wasFired = true;
        }

        return read;

    }
    public void ReadLoop()
    {
        int read = 0;
        //int initialLength = 176400;
        int initialLength = 819200;
        int length = initialLength;
        byte[] completeBuffer = new byte[initialLength];


        while (!Closed)
        {

            byte[] buffer = new byte[length];
            int chunk = stream.Read(buffer, 0, length);
            read += chunk;


            if (chunk == 0)
            {
                /*Closed = true;
				Console.WriteLine("BREAKING!!");*/
            }
            else if (read < initialLength)
            {

                length -= chunk;
                buffer.CopyTo(completeBuffer, read - chunk);
            }
            else
            {
                buffer.CopyTo(completeBuffer, read - chunk);
                length = initialLength;
                read = 0;


                var rbuffer = resampler.getResample(completeBuffer);


                pendingSeconds.Enqueue(rbuffer);


                Array.Clear(completeBuffer, 0, completeBuffer.Length);
                if (wasFired)
                {
                    player.Play(this);
                    wasFired = false;
                }

                if (pendingSeconds.Count >= 2)
                {
                    wait.WaitOne();
                }
            }
        }
    }

    
    private Stream getCurrentStream()
    {
        if (useResampledStream)
        {
            return resampledStream;
        } else
        {
            return stream;
        }
    }


    public void start()
    {
        Closed = false;
        mainReadLoop = new Thread(() => this.ReadLoop());
        mainReadLoop.Start();
    }
    public void setResample(bool on)
    {
        resampler.doResample = on;
    }
    public void startTestFile()
    {
        Closed = false;
        useResampledStream = false;
    }
    public void startTestSample()
    {
        Closed = false;
        mainReadLoop = new Thread(() => this.ReadLoop());
        mainReadLoop.Start();
    }



    public void Dispose()
    {
        if (Closed) return;

        Closed = true;
        wait.Set();

        mainReadLoop?.Join();
        stream?.Close();
        resampledStream?.Close();
        resampler = null;
    }




    private class Resampler
    {
        private bool isWindows;
        WaveFormat oldFormat = new WaveFormat(44100, 16, 2);
        WaveFormat newFormat = new WaveFormat(48000, 16, 2);
        public bool doResample = true;

        public Resampler()
        {
            //isWindows = OperatingSystem.IsWindows();
            isWindows = false;
        }
        public byte[] getResample(byte[] pcm) {
            if (!doResample) return pcm;
            if(isWindows)
            {
                return ResampleWindows(pcm);
            } else
            {
                return ResampleLinux(pcm);
            }

        }
        private byte[] ResampleWindows(byte[] pcm)
        {
            using (MemoryStream mem = new MemoryStream(pcm))
            {
                using (RawSourceWaveStream stream = new RawSourceWaveStream(mem, oldFormat))
                {

                    using(WaveFormatConversionStream resampler = new WaveFormatConversionStream(newFormat, stream))
                    {

                        int resampled_length = (int)((float)pcm.Length * ((float)newFormat.SampleRate / (float)oldFormat.SampleRate));

                        byte[] ret = new byte[resampled_length];
                        resampler.Read(ret, 0, resampled_length);
                        return ret;
                    }
                }
            }
        }
        private byte[] ResampleLinux(byte[] pcm)
        {
            
            using (MemoryStream mem = new MemoryStream(pcm))
            {
                using (RawSourceWaveStream stream = new RawSourceWaveStream(mem, oldFormat))
                {
                    Pcm16BitToSampleProvider sampleProvider = new Pcm16BitToSampleProvider(stream);
                    WdlResamplingSampleProvider resampedWavFile = new WdlResamplingSampleProvider(sampleProvider, newFormat.SampleRate);
                    SampleToWaveProvider16 sampleToWavProvider = new SampleToWaveProvider16(resampedWavFile);

                    int resampled_length = (int)((float)pcm.Length * ((float)newFormat.SampleRate / (float)oldFormat.SampleRate));
                    byte[] ret = new byte[resampled_length];
                    sampleToWavProvider.Read(ret, 0, resampled_length);
                    return ret;
                }
            }
        }
    }
   
}

