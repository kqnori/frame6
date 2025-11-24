using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using OpenCvSharp;
using Accord;
using Accord.Video.FFMPEG;
using System.Threading;
using System.Linq;
using NAudio.Wave;
using FFMpegCore;
using FFMpegCore.Pipes;

class Program
{
    static void Main(string[] args)
    {
        if (Directory.Exists("save"))
            Directory.Delete("save", true);
        Directory.CreateDirectory("save");

        if (!Directory.Exists("save/after"))
            Directory.CreateDirectory("save/after");


        string videoPath = args[0];
        int fps;
        int duration;
        using (VideoCapture capture = new OpenCvSharp.VideoCapture(videoPath))
        {
            fps = (int)Math.Round((float)capture.Fps);
            duration = (int)(capture.FrameCount / fps) - 1;
        }

        Console.Write($"How meny sec of new vid(max:{duration+1}): ");
        string strklatek = Console.ReadLine();
        int klatek;
        Console.Write($"How meny fps of new vid(max:{fps}) : ");
        string strfpsined = Console.ReadLine();
        int fpsinend;

        Console.Write("tolerancja nowego (domyslnie 15): ");
        string strtolerancja = Console.ReadLine();
        int tolerancja;


        klatek = string.IsNullOrEmpty(strklatek) ? duration : int.Parse(strklatek);

        fpsinend = string.IsNullOrEmpty(strfpsined) ? fps : int.Parse(strfpsined) ;

        tolerancja = string.IsNullOrEmpty(strtolerancja) ? 15 : int.Parse(strtolerancja);

        int ileklatek = (int)(fpsinend * klatek);

        int coktoraklatka = (int)(fps / (fpsinend));

        Console.WriteLine($"tol:{tolerancja} fps:{fpsinend} sekund:{klatek} ileklatek:{ileklatek} coktora:{coktoraklatka}");
        Thread.Sleep(1000);
        //Console.ReadLine();


        Parallel.For(0, ileklatek, i =>
        {
            string framePath = $"save/image{i + 1}.jpg";
            int frameNumber = coktoraklatka * (i + 1);
            ExtractFrame(videoPath, framePath, frameNumber);
        });

        OpenCvSharp.Mat[] frames = new OpenCvSharp.Mat[ileklatek];
        Parallel.For(0, ileklatek, i =>
        {
            frames[i] = Cv2.ImRead($"save/image{i + 1}.jpg", OpenCvSharp.ImreadModes.Grayscale);
        });

        Parallel.For(0, ileklatek - 1, i =>
        {
            OpenCvSharp.Mat diff = new OpenCvSharp.Mat();
            Cv2.Absdiff(frames[i], frames[i + 1], diff);
            Cv2.Threshold(diff, diff, tolerancja, 255, ThresholdTypes.Binary);
            Cv2.ImWrite($"save/after/image{i + 1}.jpg", diff);
            Console.WriteLine("one file done: " + i);
        });
        Thread.Sleep(100);
        CreateGifFromImages(ileklatek, fpsinend, videoPath);
        Thread.Sleep(100);
        Directory.Delete("save", true);
    }

    public static void ExtractFrame(string videoPath, string framePath, int frameNumber)
    {
        using (VideoCapture capture = new OpenCvSharp.VideoCapture(videoPath))
        {

            capture.Set(VideoCaptureProperties.PosFrames, frameNumber);
            using (Mat frame = new OpenCvSharp.Mat())
            {
                if (capture.Read(frame))
                {
                    Cv2.ImWrite(framePath, frame);
                    Console.WriteLine("Frame exported successfully! " + framePath);
                }
                else
                {
                    Console.WriteLine("Failed to read the frame.");
                }
            }
        }
    }

    public static void CreateGifFromImages(int loop, int fps, string nameoffile)
    {
        string[] files = new string[loop - 1];
        for (int i = 0; i < loop - 1; i++)
        {
            files[i] = $"save/after/image{i + 1}.jpg";
        }
        int width;
        int height;
        using (Bitmap firstImage = new Bitmap(files[0]))
        {
            width = firstImage.Width;
            height = firstImage.Height;
        }

        string tempVideoPath = Path.GetFileName(nameoffile).Split('.').First() + "~temp.mp4";
        using (VideoFileWriter videoFileWriter = new VideoFileWriter())
        {
            videoFileWriter.Open(tempVideoPath, width, height, fps, VideoCodec.H264);

            foreach (string file in files)
            {
                using (Bitmap image = (Bitmap)System.Drawing.Image.FromFile(file))
                {
                    videoFileWriter.WriteVideoFrame(image);
                }
            }

            videoFileWriter.Close();
        }

        string audioPath = Path.GetFileName(nameoffile).Split('.').First() + "~audio.wav";
        ExtractAudio(nameoffile, audioPath);

        CombineAudioAndVideo(tempVideoPath, audioPath, Path.GetFileName(nameoffile).Split('.').First() + "~done.mp4");

        File.Delete(tempVideoPath);
        File.Delete(audioPath);

    }

    public static void ExtractAudio(string videoPath, string audioOutputPath)
    {
        using (var reader = new MediaFoundationReader(videoPath))
        {
            WaveFileWriter.CreateWaveFile(audioOutputPath, reader);
        }
    }

    public static void CombineAudioAndVideo(string videoPath, string audioPath, string outputPath)
    {
            var video = FFMpegArguments.FromFileInput(videoPath)
                                       .AddFileInput(audioPath)
                                       .OutputToFile(outputPath, true, options => options
                                           .WithVideoCodec("copy")
                                           .WithAudioCodec("aac")
                                           .WithCustomArgument("-shortest"))
                                       .ProcessSynchronously();
    }



}
