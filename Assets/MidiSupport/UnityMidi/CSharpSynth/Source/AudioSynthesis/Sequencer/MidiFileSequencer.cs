﻿/*
 *    ______   __ __     _____             __  __  
 *   / ____/__/ // /_   / ___/__  ______  / /_/ /_ 
 *  / /    /_  _  __/   \__ \/ / / / __ \/ __/ __ \
 * / /___ /_  _  __/   ___/ / /_/ / / / / /_/ / / /
 * \____/  /_//_/     /____/\__, /_/ /_/\__/_/ /_/ 
 *                         /____/                  
 * Midi File Sequencer 
 *  Used for situations where the whole midi is available in file format locally or over a network stream.
 *  Loads the midi and calculates the timing before hand so when sequencing no BPM calculation is needed.
 */
using System;
using AudioSynthesis.Midi;
using AudioSynthesis.Synthesis;
using AudioSynthesis.Midi.Event;
using System.Collections.Generic;

namespace AudioSynthesis.Sequencer
{
    public class MidiFileSequencer
    {
        //--Variables
        private Synthesizer synth;
        private MidiMessage[] mdata;
        private List<MidiMessage> mdataList = new List<MidiMessage>();
        private bool[] blockList;
        private bool playing = false;
        private double playbackrate = 1.0; // 1/8 to 8
        private int totalTime;
        private int sampleTime;
        private int eventIndex;

        //--Public Properties
        public Synthesizer Synth
        {
            get { return synth; }
            set { synth = value; }
        }
        public bool IsPlaying
        {
            get { return playing; }
        }
        public bool IsMidiLoaded
        {
            get { return mdata != null; }
        }
        public MidiMessage[] Data
        {
            get { return mdata; }
        }
        public int CurrentTime
        {
            get { return sampleTime; }
        }
        public int EndTime
        {
            get { return totalTime; }
        }
        public double PlaySpeed
        {
            get { return playbackrate; }
            set { playbackrate = SynthHelper.Clamp(value, .125, 8.0); }
        }

        //--Public Methods
        public MidiFileSequencer(Synthesizer synth)
        {
            this.synth = synth;
            blockList = new bool[Synthesizer.DefaultChannelCount];
        }

        public bool LoadMidi(IResource midiFile)
        {
            if (playing == true)
                return false;
            LoadMidiFile(new MidiFile(midiFile));
            return true;
        }
        public bool LoadMidi(MidiFile midiFile)
        {
            if (playing == true)
                return false;
            LoadMidiFile(midiFile);
            return true;
        }

        public bool LoadMidiTrack(MidiTrack midiTrack, double BPM, int division)
        {
            if (playing == true)
                return false;
            LoadMidiTrackMessages(midiTrack, BPM, division);
            return true;
        }
        public bool LoadMidiEventAtRunTime(MidiEvent midiEvent, double BPM, int division, int triggerDeltaTime)
        {
            //We can load Midi events at run-time
            LoadMidiMessageAtRunTime(midiEvent, BPM, division, triggerDeltaTime);
            return true;
        }
        public bool UnloadMidi()
        {
            if (playing == true)
                return false;
            mdata = null;
            return true;
        }
        public void Play()
        {
            if (playing == true || (mdata == null && mdataList.Count == 0))
                return;
            playing = true;
        }
        public void Stop()
        {
            playing = false;
            sampleTime = 0;
            eventIndex = 0;
        }
        public void Pause()
        {
            playing = false;
        }
        public bool IsChannelMuted(int channel)
        {
            return blockList[channel];
        }
        public void MuteAllChannels()
        {
            for (int x = 0; x < blockList.Length; x++)
                blockList[x] = true;
        }
        public void UnMuteAllChannels()
        {
            Array.Clear(blockList, 0, blockList.Length);
        }
        public void SetMute(int channel, bool muteValue)
        {
            blockList[channel] = muteValue;
        }
        public void Seek(TimeSpan time)
        {
            int targetSampleTime = (int)(synth.SampleRate * time.TotalSeconds);
            if (targetSampleTime > sampleTime)
            {//process forward
                SilentProcess(targetSampleTime - sampleTime);
            }
            else if (targetSampleTime < sampleTime)
            {//we have to restart the midi to make sure we get the right state: instruments, volume, pan, etc
                sampleTime = 0;
                eventIndex = 0;
                synth.NoteOffAll(true);
                synth.ResetPrograms();
                synth.ResetSynthControls();
                SilentProcess(targetSampleTime);
            }
        }
        public void FillMidiEventQueue()
        {
            if (!playing || synth.midiEventQueue.Count != 0)
                return;
            if (sampleTime >= totalTime)
            {
                sampleTime = 0;
                eventIndex = 0;
                playing = false;
                synth.NoteOffAll(true);
                synth.ResetPrograms();
                synth.ResetSynthControls();
                return;
            }
            int newMSize = (int)(synth.MicroBufferSize * playbackrate);       
            for (int x = 0; x < synth.midiEventCounts.Length; x++)
            {
                sampleTime += newMSize;
                while (eventIndex < mdata.Length && mdata[eventIndex].delta < sampleTime)
                {
                    if (mdata[eventIndex].command != 0x90 || blockList[mdata[eventIndex].channel] == false)
                    {
                        synth.midiEventQueue.Enqueue(mdata[eventIndex]);
                        synth.midiEventCounts[x]++;
                    }
                    eventIndex++;
                }
            }
        }
        public void FillMidiEventQueueTillPause(double timeToStop)
        {
            if (!playing || synth.midiEventQueue.Count != 0)
                return;
            if (sampleTime >= totalTime)
            {
                sampleTime = 0;
                eventIndex = 0;
                playing = false;
                synth.NoteOffAll(true);
                synth.ResetPrograms();
                synth.ResetSynthControls();
                return;
            }
            if (sampleTime >= timeToStop)
            {
                return;
            }
            int newMSize = (int)(synth.MicroBufferSize * playbackrate);
            for (int x = 0; x < synth.midiEventCounts.Length; x++)
            {
                sampleTime += newMSize;
                while (eventIndex < mdata.Length && mdata[eventIndex].delta < sampleTime)
                {
                    if (mdata[eventIndex].command != 0x90 || blockList[mdata[eventIndex].channel] == false)
                    {
                        synth.midiEventQueue.Enqueue(mdata[eventIndex]);
                        synth.midiEventCounts[x]++;
                    }
                    eventIndex++;
                }
            }
        }
        //Handles the events being loaded one by one by MidiPlayOnInput at run-time
        public void FillMidiEventQueueAsyncLoad()
        {
            if (!playing)
                return;
            if (sampleTime >= totalTime)
            {
                sampleTime = 0;
                eventIndex = 0;
                totalTime = 0;
                playing = false;
                mdataList.Clear();
                return;
            }
            int newMSize = (int)(synth.MicroBufferSize * playbackrate);
            for (int x = 0; x < synth.midiEventCounts.Length; x++)
            {
                sampleTime += newMSize;
                int indexToCheck = 0;
                while(indexToCheck < mdataList.Count)
                {
                    if(mdataList[indexToCheck].delta < sampleTime)
                    {
                        //Events are not sequence
                        if (mdataList[indexToCheck].command != 0x90 || blockList[mdataList[indexToCheck].channel] == false)
                        {
                            synth.midiEventQueue.Enqueue(mdataList[indexToCheck]);
                            synth.midiEventCounts[x]++;
                            mdataList.RemoveAt(indexToCheck);
                        }
                        else
                        {
                            indexToCheck++;
                        }
                    }
                    else
                    {
                        indexToCheck++;
                    }
                }
            }
        }
        private void LoadMidiMessageAtRunTime(MidiEvent mEvent, double BPM, int division, int triggerDeltaTime)
        {
            double deltaTime = synth.SampleRate * triggerDeltaTime * (60.0 / (BPM * division));
            MidiMessage midiMessage = new MidiMessage((byte)mEvent.Channel, (byte)mEvent.Command, (byte)mEvent.Data1, (byte)mEvent.Data2);
            midiMessage.delta = CurrentTime + (int)deltaTime;
            mdataList.Add(midiMessage);

            if (totalTime < midiMessage.delta)
            {
                  totalTime = midiMessage.delta;
            }
        }
            //--Private Methods
        private void LoadMidiTrackMessages(MidiTrack midiTrack, double BPM, int division)
        {
            mdata = new MidiMessage[midiTrack.MidiEvents.Length];
            double absDelta = 0.0;
            for (int x = 0; x < mdata.Length; x++)
            {
                MidiEvent mEvent = midiTrack.MidiEvents[x];
                mdata[x] = new MidiMessage((byte)mEvent.Channel, (byte)mEvent.Command, (byte)mEvent.Data1, (byte)mEvent.Data2);
                absDelta += synth.SampleRate * mEvent.DeltaTime * (60.0 / (BPM * division));
                mdata[x].delta = (int)absDelta;
                //Update tempo
                if (mEvent.Command == 0xFF && mEvent.Data1 == 0x51)
                    BPM = Math.Round(MidiHelper.MicroSecondsPerMinute / (double)((MetaNumberEvent)mEvent).Value, 2);
            }
            //Set total time to proper value
            totalTime = mdata[mdata.Length - 1].delta;
        }

        private void LoadMidiFile(MidiFile midiFile)
        {
            //Converts midi to sample based format for easy sequencing
            double BPM = midiFile.BPM;
            //Combine all tracks into 1 track that is organized from lowest to highest absolute time
            if(midiFile.Tracks.Length > 1 || midiFile.Tracks[0].EndTime == 0)
                midiFile.CombineTracks();
            mdata = new MidiMessage[midiFile.Tracks[0].MidiEvents.Length];
            //Convert delta time to sample time
            eventIndex = 0;
            sampleTime = 0;
            //Calculate sample based time using double counter and round down to nearest integer sample.
            double absDelta = 0.0;
            for (int x = 0; x < mdata.Length; x++)
            {
                MidiEvent mEvent = midiFile.Tracks[0].MidiEvents[x];
                mdata[x] = new MidiMessage((byte)mEvent.Channel, (byte)mEvent.Command, (byte)mEvent.Data1, (byte)mEvent.Data2);
                absDelta += synth.SampleRate * mEvent.DeltaTime * (60.0 / (BPM * midiFile.Division));
                mdata[x].delta = (int)absDelta;
                //Update tempo
                if (mEvent.Command == 0xFF && mEvent.Data1 == 0x51)
                    BPM = Math.Round(MidiHelper.MicroSecondsPerMinute / (double)((MetaNumberEvent)mEvent).Value, 2);
            }
            //Set total time to proper value
            totalTime = mdata[mdata.Length - 1].delta;
        }
        private void SilentProcess(int amount)
        {
            while (eventIndex < mdata.Length && mdata[eventIndex].delta < (sampleTime + amount))
            {
                if (mdata[eventIndex].command != 0x90)
                {
                    MidiMessage m = mdata[eventIndex]; 
                    synth.ProcessMidiMessage(m.channel, m.command, m.data1, m.data2);
                }
                eventIndex++;
            }
            sampleTime += amount;
        }
    }
}
