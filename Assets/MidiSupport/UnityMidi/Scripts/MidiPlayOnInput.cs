using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AudioSynthesis.Bank;
using AudioSynthesis.Synthesis;
using AudioSynthesis.Sequencer;
using AudioSynthesis.Midi;
using AudioSynthesis.Midi.Event;

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
    }
    public class MidiPlayOnInput : MonoBehaviour
    {
        [HideInInspector] MidiFileSequencer sequencer;
        [SerializeField] StreamingAssetResouce bankSource;
        [SerializeField] StreamingAssetResouce midiSource;
        int channel = 2;
        int sampleRate = 48000;
        int bufferSize = 512;
        [HideInInspector] public MidiTrackOnInputPlayback midiOnInputPlayback = new MidiTrackOnInputPlayback();
        Dictionary<string, int> synthPrograms = new Dictionary<string, int>();
        PatchBank bank;
        MidiFile midi;
        MidiTrack currentPlaybackTrack;
        int midiMessageIndex = 0;
        int midiDeltaTimeErrorMargen = 5;
        Synthesizer synthesizer;
        AudioSource audioSource;
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
            VizualizeAssociatedMidi();
            currentPlaybackTrack = midiOnInputPlayback.midiTracks[midiOnInputPlayback.midiTrackNames[midiOnInputPlayback.trackIndex]];
            //sequencer.LoadMidiTrack(track, midiFile.BPM, midiFile.Division);
            LoadBank(new PatchBank(bankSource));
            sequencer.Synth.SetProgram(midiOnInputPlayback.trackIndex, midiOnInputPlayback.synthIndex);
            sequencer.Play();
            InitMidiEvents();
        }
        void Update()
        {
            if (Input.GetKeyDown("space") || Input.GetMouseButtonDown(0))
            {
                if (!endOfTrackReached)
                {
                    if(sendIndidualEventsOnInput)
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
                        Debug.Log(nextMidiEvent.ToString() + " " + MidiFile.getNoteName(nextMidiEvent.Data1) + " Time: "+ deltaTimeTillOffNote);
                    if(deltaTimeTillOffNote > 99999 || deltaTimeTillOffNote < 0)
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
        public void LoadBank(PatchBank bank)
        {
            this.bank = bank;
            synthesizer.UnloadBank();
            synthesizer.LoadBank(bank);
            if (synthPrograms.Count == 0)
            {
                VisuzlizeBank(bank);
            }
        }

        public void VisuzlizeBank(PatchBank bank)
        {
            synthPrograms.Clear();
            int bankNmber = 0;
            while (bank.IsBankLoaded(bankNmber))
            {
                int patchNumber = 0;
                while (bank.GetPatch(bankNmber, patchNumber) != null)
                {
                    synthPrograms.Add(patchNumber + 1 + "." + bank.GetPatchName(bankNmber, patchNumber), patchNumber);
                    patchNumber++;
                }
                bankNmber++;
            }
            UpdateSynthProgramSelection();
        }

        public void VizualizeAssociatedMidi()
        {
            MidiFile midi = new MidiFile(midiSource);
            VizualizeMidiTracks(midi);
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
        public void VizualizeMidiTracks(MidiFile midi)
        {
            this.midi = midi;
            string trackName = "null";
            int trackindex = 1;
            ClearMidiTracks();

            foreach (MidiTrack track in midi.Tracks)
            {
                int count = 0;
                bool hasNotes = false;
                while (count < track.MidiEvents.Length)
                {
                    MidiEvent midiEvent = track.MidiEvents[count];

                    //Name the Track correctly based on the instrument name or Track name Midi Message
                    if (midiEvent.Data1 == 0x03 || midiEvent.Data1 == 0x04)
                    {
                        MetaTextEvent metaTextEvent = (MetaTextEvent)midiEvent;
                        trackName = metaTextEvent.Text;
                    }
                    else if (midiEvent.Command == 0x90 || midiEvent.Command == 0x80)
                    {
                        hasNotes = true;
                    }
                    count++;
                }

                if (hasNotes)
                {
                   trackName = trackindex + "."+ trackName;
                   midiOnInputPlayback.midiTracks.Add(trackName, track);
                   trackindex++;
                }
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

        public void UpdateSynthProgramSelection()
        {
            if (synthPrograms.Count > 0)
            {
                var stringKeys = new string[synthPrograms.Count];
                Dictionary<string, int>.KeyCollection keys = synthPrograms.Keys;
                int i = 0;
                foreach (string key in keys)
                {
                    stringKeys[i] = key;
                    i++;
                }

                midiOnInputPlayback.synthPrograms = stringKeys;
            }
        }

        public void OnEditorLoadMidiClicked()
        {
            VizualizeAssociatedMidi();
            VisuzlizeBank(new PatchBank(bankSource));
            midiloaded = true;
        }

        public void OnEditorUnloadMidiClicked()
        {
            midiloaded = false;
            ClearMidiTracks();
            synthPrograms.Clear();
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