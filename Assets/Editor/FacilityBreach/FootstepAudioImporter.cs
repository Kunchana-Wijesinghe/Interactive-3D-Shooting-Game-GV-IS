using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FacilityBreach.Editor
{
    /// <summary>
    /// GV Part 2: Imports and slices user-provided recorded footstep MP3 into individual clean footstep audio clips.
    /// Preserves original acoustic fidelity while extracting distinct steps for cadence playback.
    /// </summary>
    public static class FootstepAudioImporter
    {
        [MenuItem("FacilityBreach/Process Footstep Audio")]
        public static bool ProcessFootstepAudio()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            string mp3Path = "Assets/Audio/Footsteps/foot_Steps_sound.mp3";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(mp3Path);
            if (clip == null)
            {
                Debug.LogWarning($"[FootstepAudioImporter] Clip at '{mp3Path}' not yet imported by AssetDatabase.");
                return false;
            }

            Debug.Log($"[FootstepAudioImporter] Loaded '{mp3Path}': Channels={clip.channels}, Freq={clip.frequency}Hz, Length={clip.length:F2}s, Samples={clip.samples}");

            float[] rawSamples = new float[clip.samples * clip.channels];
            if (!clip.GetData(rawSamples, 0))
            {
                Debug.LogWarning("[FootstepAudioImporter] Failed to GetData from clip.");
                return false;
            }

            int channels = clip.channels;
            int sampleRate = clip.frequency;
            int totalFrames = clip.samples;

            // Convert to mono for analysis
            float[] mono = new float[totalFrames];
            for (int i = 0; i < totalFrames; i++)
            {
                float sum = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    sum += rawSamples[i * channels + ch];
                }
                mono[i] = sum / channels;
            }

            // Find peak amplitude across whole file
            float maxAmp = 0f;
            for (int i = 0; i < totalFrames; i++)
            {
                float abs = Mathf.Abs(mono[i]);
                if (abs > maxAmp) maxAmp = abs;
            }
            Debug.Log($"[FootstepAudioImporter] Max amplitude: {maxAmp:F3}");

            // Moving RMS energy window (20ms window)
            int windowSize = Mathf.Max(1, sampleRate / 50); // 20ms
            int numWindows = totalFrames / windowSize;
            float[] rms = new float[numWindows];
            for (int w = 0; w < numWindows; w++)
            {
                float sumSq = 0f;
                int start = w * windowSize;
                for (int j = 0; j < windowSize; j++)
                {
                    float s = mono[start + j];
                    sumSq += s * s;
                }
                rms[w] = Mathf.Sqrt(sumSq / windowSize);
            }

            // Peak detection on RMS with a minimum spacing of 0.25s
            int minWindowSpacing = (int)(0.25f * sampleRate / windowSize);
            float rmsThreshold = maxAmp * 0.15f;

            List<int> stepPeakWindows = new List<int>();
            for (int w = 1; w < numWindows - 1; w++)
            {
                if (rms[w] > rmsThreshold && rms[w] >= rms[w - 1] && rms[w] >= rms[w + 1])
                {
                    if (stepPeakWindows.Count == 0 || (w - stepPeakWindows[stepPeakWindows.Count - 1]) >= minWindowSpacing)
                    {
                        stepPeakWindows.Add(w);
                    }
                    else if (rms[w] > rms[stepPeakWindows[stepPeakWindows.Count - 1]])
                    {
                        stepPeakWindows[stepPeakWindows.Count - 1] = w;
                    }
                }
            }

            Debug.Log($"[FootstepAudioImporter] Detected {stepPeakWindows.Count} distinct step peaks in recording.");
            for (int k = 0; k < stepPeakWindows.Count; k++)
            {
                float timeSec = (float)stepPeakWindows[k] * windowSize / sampleRate;
                Debug.Log($"  Step #{k + 1}: Time={timeSec:F2}s, RMS={rms[stepPeakWindows[k]]:F3}");
            }

            // Extract 4 clean steps (or if < 4, extract as many as available; if only 1, extract the whole step)
            int extractCount = Mathf.Clamp(stepPeakWindows.Count, 1, 4);
            int stepDurationFrames = (int)(0.35f * sampleRate); // 350ms duration per step
            int leadInFrames = (int)(0.03f * sampleRate);       // 30ms lead-in before peak
            int fadeInFrames = (int)(0.005f * sampleRate);      // 5ms fade in
            int fadeOutFrames = (int)(0.05f * sampleRate);      // 50ms fade out

            string resDir = "Assets/Resources/Audio/Footsteps";
            string audioDir = "Assets/Audio/Footsteps";

            if (!Directory.Exists(resDir)) Directory.CreateDirectory(resDir);
            if (!Directory.Exists(audioDir)) Directory.CreateDirectory(audioDir);

            for (int s = 0; s < extractCount; s++)
            {
                int peakFrame = stepPeakWindows[s] * windowSize;
                int startFrame = Mathf.Max(0, peakFrame - leadInFrames);
                int endFrame = Mathf.Min(totalFrames, startFrame + stepDurationFrames);
                int count = endFrame - startFrame;

                float[] slice = new float[count];
                float sliceMax = 0f;
                for (int i = 0; i < count; i++)
                {
                    float val = mono[startFrame + i];

                    // Fade in
                    if (i < fadeInFrames)
                    {
                        val *= (float)i / fadeInFrames;
                    }
                    // Fade out
                    int fromEnd = count - 1 - i;
                    if (fromEnd < fadeOutFrames)
                    {
                        val *= (float)fromEnd / fadeOutFrames;
                    }

                    slice[i] = val;
                    float abs = Mathf.Abs(val);
                    if (abs > sliceMax) sliceMax = abs;
                }

                // Normalize slice peak around 0.85
                if (sliceMax > 0.01f)
                {
                    float normFactor = 0.85f / sliceMax;
                    for (int i = 0; i < count; i++)
                    {
                        slice[i] *= normFactor;
                    }
                }

                byte[] wavBytes = EncodeWav(slice, 1, sampleRate);
                string resPath = $"{resDir}/footstep_0{s + 1}.wav";
                string directPath = $"{audioDir}/footstep_0{s + 1}.wav";

                File.WriteAllBytes(resPath, wavBytes);
                File.WriteAllBytes(directPath, wavBytes);
                Debug.Log($"[FootstepAudioImporter] Exported '{resPath}' ({count} samples, {count / (float)sampleRate:F3}s)");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[FootstepAudioImporter] Slicing & Import Complete!");
            return true;
        }

        private static byte[] EncodeWav(float[] samples, int channels, int sampleRate)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                int bytesPerSample = 2; // 16-bit
                int byteRate = sampleRate * channels * bytesPerSample;
                int blockAlign = channels * bytesPerSample;
                int subChunk2Size = samples.Length * bytesPerSample;

                // RIFF header
                writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + subChunk2Size);
                writer.Write(new char[4] { 'W', 'A', 'V', 'E' });

                // fmt chunk
                writer.Write(new char[4] { 'f', 'm', 't', ' ' });
                writer.Write(16);               // Subchunk1Size for PCM
                writer.Write((short)1);          // AudioFormat = PCM
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write((short)blockAlign);
                writer.Write((short)16);         // BitsPerSample

                // data chunk
                writer.Write(new char[4] { 'd', 'a', 't', 'a' });
                writer.Write(subChunk2Size);

                for (int i = 0; i < samples.Length; i++)
                {
                    short val = (short)Mathf.Clamp(samples[i] * 32767f, -32768f, 32767f);
                    writer.Write(val);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}
