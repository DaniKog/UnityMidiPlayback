using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AudioSynthesis.Bank;
using AudioSynthesis.Synthesis;
using AudioSynthesis.Sequencer;
using AudioSynthesis.Midi;
using AudioSynthesis.Midi.Event;
using System;

namespace UnityMidi
{
    [RequireComponent(typeof(AudioSource))]
    [System.Serializable]
    public class MidiTrackOnInputPlayback
    {
        public Dictionary<string, MidiTrack> midiTracks;
        [HideInInspector] public string[] synthPrograms;
        [HideInInspector] public string[] midiTrackNames;
        [HideInInspector] public int synthIndex;
        [HideInInspector] public int trackIndex;
        [HideInInspector] public string[] banks;
        [HideInInspector] public int bankIndex;
        [HideInInspector] public bool drumTrack;
    }
    public class MidiPlayOnInput : UnityMidiPlayer
    {
        [HideInInspector] public MidiTrackOnInputPlayback midiOnInputPlayback = new MidiTrackOnInputPlayback();

        MidiTrack currentPlaybackTrack;
        int midiMessageIndex = 0;
        int midiDeltaTimeErrorMargen = 5;

        int bufferHead;
        float[] currentBuffer;
        [HideInInspector] public bool midiloaded = false;
        public bool printToDebug = false;
        public bool sendIndidualEventsOnInput = false;
        bool endOfTrackReached = false;
        // Start is called before the first frame update
        public void Awake()
        {
            synthesizer = new Synthesizer(sampleRate, channel, bufferSize, 1);
            audioSource = GetComponent<AudioSource>();
            sequencer = new MidiFileSequencer(synthesizer);
            MidiFile midiFile = new MidiFile(midiSource);
            //Todo find a way to keep the midiTracks loaded from editor more so we don't need to reload them on Awake
            LoadBank(new PatchBank(bankSource));
            VizualizeAssociatedMidi(false);
            currentPlaybackTrack = midiOnInputPlayback.midiTracks[midiOnInputPlayback.midiTrackNames[midiOnInputPlayback.trackIndex]];

            sequencer.Play();

            if (midiOnInputPlayback.drumTrack == true)
            {
                sequencer.Synth.SetDrumChannel(midiOnInputPlayback.trackIndex);
            }
            else
            {
                sequencer.Synth.UnSetDrumChannel(midiOnInputPlayback.trackIndex);
            }

            sequencer.Synth.SetProgram(midiOnInputPlayback.trackIndex, midiOnInputPlayback.synthIndex);
            InitMidiEvents();
        }
        void Update()
        {
            if (Input.GetKeyDown("space") || Input.GetMouseButtonDown(0))
            {
                if (!endOfTrackReached)
                {
                    if (sendIndidualEventsOnInput)
                    {
                        //Bypasses playback logic and just sends the next midi event in the list. Has no considiration for BPM nor Divisions
                        SendNextMidiEvent(currentPlaybackTrack.MidiEvents[midiMessageIndex]);
                    }
                    else
                    {
                        //Will send the note on and calcualte the next off event and couple them to together.Takes into account the BPM nor Divisions for note length
                        SendNextNoteOnEvent(currentPlaybackTrack.MidiEvents[midiMessageIndex]);
                    }

                    sequencer.Play();
                }
                else if (printToDebug)
                {
                    Debug.Log("End of MidiTrack " + midiOnInputPlayback.midiTrackNames[midiOnInputPlayback.trackIndex]);
                }
            }
        }

        public void InitMidiEvents()
        {
            if (!endOfTrackReached)
            {
                //Send all the initial Syth setup midi event before playing any notes
                MidiEvent midiEvent = currentPlaybackTrack.MidiEvents[midiMessageIndex];
                if (midiEvent.Command != 0x90 && midiEvent.Command != 0x80)
                {
                    SendNextMidiEvent(midiEvent);
                    InitMidiEvents();
                }
            }
        }
        public void SendNextNoteOffEventForNote(int noteNumber)
        {
            int deltaTimeTillOffNote = 0;
            for (int i = midiMessageIndex; i < currentPlaybackTrack.MidiEvents.Length; i++)
            {
                //Creating a copy to not change any values in side the currentPlaybackTrack
                MidiEvent nextMidiEvent = currentPlaybackTrack.MidiEvents[i];
                deltaTimeTillOffNote += nextMidiEvent.DeltaTime;
                //Only send note off
                if (nextMidiEvent.Command == 0x80 && nextMidiEvent.Data1 == noteNumber)
                {
                    sequencer.LoadMidiEventAtRunTime(nextMidiEvent, midi.BPM, midi.Division, deltaTimeTillOffNote);
                    if (printToDebug)
                        Debug.Log(nextMidiEvent.ToString() + " " + MidiFile.getNoteName(nextMidiEvent.Data1) + " Time: " + deltaTimeTillOffNote);
                    if (deltaTimeTillOffNote > 99999 || deltaTimeTillOffNote < 0)
                        Debug.Log("STOP!");
                    break;
                }
            }
        }

        public void SendNextNoteOnEvent(MidiEvent midiEvent)
        {
            //Check for End of Track Message
            if (midiEvent.Command == 0xFF && midiEvent.Data1 == 0x2F)
            {
                endOfTrackReached = true;
                if (printToDebug)
                    Debug.Log("End of MidiTrack " + midiOnInputPlayback.midiTrackNames[midiOnInputPlayback.trackIndex]);
            }
            else
            {
                //Already Get the next event
                midiMessageIndex++;
                MidiEvent nextMidiEvent = currentPlaybackTrack.MidiEvents[midiMessageIndex];

                //Only send note on
                if (midiEvent.Command == 0x90)
                {
                    sequencer.LoadMidiEventAtRunTime(midiEvent, midi.BPM, midi.Division, 0);
                    if (printToDebug)
                        Debug.Log(midiEvent.ToString() + " " + MidiFile.getNoteName(midiEvent.Data1) + " Time: " + 0);

                    SendNextNoteOffEventForNote(midiEvent.Data1);

                    //Handle Chords
                    //Sometimes some notes in the chord are not at 0 delta time so midiDeltaTimeErrorMargen handles that
                    if (nextMidiEvent.Command == 0x90 && nextMidiEvent.DeltaTime <= midiDeltaTimeErrorMargen)
                    {
                        SendNextNoteOnEvent(nextMidiEvent);
                    }
                }
                else
                {
                    SendNextNoteOnEvent(nextMidiEvent);
                }
            }

        }

        public void SendNextMidiEvent(MidiEvent midiEvent)
        {
            MidiMessage midiMsg = new MidiMessage((byte)midiEvent.Channel, (byte)midiEvent.Command, (byte)midiEvent.Data1, (byte)midiEvent.Data2);
            midiMsg.delta = 0;
            sequencer.Synth.midiEventQueue.Enqueue(midiMsg);
            sequencer.Synth.midiEventCounts[0]++;

            if (printToDebug)
            {
                if (midiEvent.Command == 0x90 || midiEvent.Command == 0x80)
                    Debug.Log(midiEvent.ToString() + " " + MidiFile.getNoteName(midiEvent.Data1));
                else
                    Debug.Log(midiEvent.ToString());
            }

            //Check for End of Track Message
            if (midiEvent.Command == 0xFF && midiEvent.Data1 == 0x2F)
            {
                endOfTrackReached = true;
                if (printToDebug)
                    Debug.Log("End of MidiTrack " + midiOnInputPlayback.midiTrackNames[midiOnInputPlayback.trackIndex]);
            }
            else
            {
                midiMessageIndex++;
                MidiEvent nextMidiEvent = currentPlaybackTrack.MidiEvents[midiMessageIndex];
                //Check Delta time of next event to add it too to handle chords
                if (nextMidiEvent.DeltaTime == 0)
                {
                    SendNextMidiEvent(nextMidiEvent);
                }
            }
        }

        public void VizualizeAssociatedMidi(bool setDefualt)
        {
            midi = new MidiFile(midiSource);
            VizualizeMidiTracks(midi, setDefualt);
        }
        public void ClearMidiTracks()
        {
            if (midiOnInputPlayback.midiTracks != null)
            {
                midiOnInputPlayback.midiTracks.Clear();
            }
            else
            {
                midiOnInputPlayback.midiTracks = new Dictionary<string, MidiTrack>();
            }
        }
        public void VizualizeMidiTracks(MidiFile midi, bool setDefault)
        {
            this.midi = midi;
            string trackName = "Track";
            int trackindex = 1;
            ClearMidiTracks();
            MidiTrack[] midiTracksToProcess = GetTracksFromMidi(midi);
            foreach (MidiTrack track in midiTracksToProcess)
            {
                trackName = GetTrackName(track);

                if (HasNotes(track))
                {
                    trackName = trackindex + "." + trackName;
                    midiOnInputPlayback.midiTracks[trackName] =  track;
                    trackindex++;
                }
            }
            if (setDefault)
            {
                SetDefaultInstumentForTrack();
            }
            UpdateTrackNameSelection();
            UpdateSynthProgramSelection();

        }
        public void UpdateTrackNameSelection()
        {
            if (midiOnInputPlayback.midiTracks.Count > 0)
            {
                var trackNames = new string[midiOnInputPlayback.midiTracks.Count];
                Dictionary<string, MidiTrack>.KeyCollection keys = midiOnInputPlayback.midiTracks.Keys;
                int i = 0;
                foreach (string key in keys)
                {
                    trackNames[i] = key;
                    i++;
                }
                midiOnInputPlayback.midiTrackNames = trackNames;
            }
        }
        public void SetDefaultInstumentForTrack()
        {
            //Todo find a way to keep the midiTracks loaded from editor
            if(midiOnInputPlayback.midiTracks == null)
            {
                VizualizeAssociatedMidi(false);
            }
            currentPlaybackTrack = midiOnInputPlayback.midiTracks[midiOnInputPlayback.midiTrackNames[midiOnInputPlayback.trackIndex]];
            if (currentPlaybackTrack.Instruments.Length > 0)
            {
                midiOnInputPlayback.bankIndex = 0;
                midiOnInputPlayback.synthIndex = currentPlaybackTrack.Instruments[0];
                midiOnInputPlayback.drumTrack = false;
            }
            else if (currentPlaybackTrack.DrumInstruments.Length > 0)
            {
                // Usually Drum bank is 2nd bank
                midiOnInputPlayback.bankIndex = 1;
                midiOnInputPlayback.synthIndex = currentPlaybackTrack.DrumInstruments[0];
                midiOnInputPlayback.drumTrack = true;
            }
        }
        public void UpdateBankProgramSelection()
        {
            if (IsSynthBanksEmpty())
            {
                //TODO Handle edge case of Synth being cleared
                VisuzlizeBank(new PatchBank(bankSource));
            }

            //midiOnInputPlayback.synthIndex = 0;

            int bankIndex = midiOnInputPlayback.bankIndex;
            if (bankIndex == 1)
            {
                midiOnInputPlayback.drumTrack = true;
            }
            else
            {
                midiOnInputPlayback.drumTrack = false;
            }

            string bankName = midiOnInputPlayback.banks[bankIndex];
            midiOnInputPlayback.synthPrograms = GetSynthStringKeys(bankName); ;
        }
        public override void LoadBank(PatchBank bank) 
        {
            base.LoadBank(bank);
            UpdateSynthProgramSelection();
        }

        public void UpdateSynthProgramSelection()
        {
                midiOnInputPlayback.banks = GetBankStringKeys();

                int bankIndex = midiOnInputPlayback.bankIndex;
                string bankName = midiOnInputPlayback.banks[bankIndex];

                midiOnInputPlayback.synthPrograms = GetSynthStringKeys(bankName);
        }

        public void OnEditorLoadMidiClicked()
        {
            VisuzlizeBank(new PatchBank(bankSource));
            VizualizeAssociatedMidi(true);
            midiloaded = true;
        }

        public void OnEditorUnloadMidiClicked()
        {
            midiloaded = false;
            ClearMidiTracks();
            ClearSynthBanks();
        }
        public void OnEditorChangeMade_BankIndex()
        {
            UpdateBankProgramSelection();
        }
        public void OnEditorChangeMade_TrackIndex()
        {
            if (IsSynthBanksEmpty()) // Can rewrite with SerializedField
            {
                VisuzlizeBank(new PatchBank(bankSource));
                VizualizeAssociatedMidi(true);
            }
            else
            {
                SetDefaultInstumentForTrack();
                UpdateBankProgramSelection();
            }
        }

        void OnAudioFilterRead(float[] data, int channel)
        {
            Debug.Assert(this.channel == channel);
            int count = 0;
            while (count < data.Length)
            {
                if (currentBuffer == null || bufferHead >= currentBuffer.Length)
                {
                    sequencer.FillMidiEventQueueAsyncLoad();
                    synthesizer.GetNext();
                    currentBuffer = synthesizer.WorkingBuffer;
                    bufferHead = 0;
                }
                var length = Mathf.Min(currentBuffer.Length - bufferHead, data.Length - count);
                System.Array.Copy(currentBuffer, bufferHead, data, count, length);
                bufferHead += length;
                count += length;
            }
        }

    }
}