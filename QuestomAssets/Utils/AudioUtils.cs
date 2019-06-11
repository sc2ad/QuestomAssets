using QuestomAssets.AssetsChanger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace QuestomAssets.Utils
{
    public class AudioUtils
    {
        public static AudioClipObject CreateAudioClip(string audioClipFile, string outputFileName, string name, AssetsFile file)
        {
            int channels;
            int frequency;
            Single length;
            byte[] oggBytes = File.ReadAllBytes(audioClipFile);
            unsafe
            {

                GCHandle pinnedArray = GCHandle.Alloc(oggBytes, GCHandleType.Pinned);
                try
                {
                    IntPtr pointer = pinnedArray.AddrOfPinnedObject();
                    int error;
                    StbSharp.StbVorbis.stb_vorbis_alloc alloc;
                    StbSharp.StbVorbis.stb_vorbis v = StbSharp.StbVorbis.stb_vorbis_open_memory((byte*)pointer.ToPointer(), oggBytes.Length, &error, &alloc);
                    channels = v.channels;
                    frequency = (int)v.sample_rate;
                    length = StbSharp.StbVorbis.stb_vorbis_stream_length_in_seconds(v);
                    StbSharp.StbVorbis.stb_vorbis_close(v);
                }
                catch (Exception ex)
                {
                    Log.LogErr($"Exception parsing ogg file {audioClipFile}", ex);
                    return null;
                }
                finally
                {
                    pinnedArray.Free();
                }
            }
            var audioClip = new AudioClipObject(file)
            {
                Name = name,
                LoadType = 1,
                IsTrackerFormat = false,
                Ambisonic = false,
                SubsoundIndex = 0,
                PreloadAudioData = false,
                LoadInBackground = true,
                Legacy3D = true,
                CompressionFormat = 1,
                BitsPerSample = 16,
                Channels = channels,
                Frequency = frequency,
                Length = (Single)length,
                Resource = new StreamedResource(outputFileName, 0, Convert.ToUInt64(new FileInfo(audioClipFile).Length))
            };
            return audioClip;
        }
    }
}
