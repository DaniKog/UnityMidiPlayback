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

    public class MidiPlayOnInput : MonoBehaviour
    {
        [System.Serializable]
        public class MidiTrackOnInputPlayback
        {
            public Dictionary<string, MidiTrack> midiTracks;
            [HideInInspector] public string[] synthPrograms;
            [HideInInspector] public string[] midiTrackNames;
            [HideInInspector] public int synthIndex;
            [HideInInspector] public int trackIndex;
        }

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
        Synthesizer synthesizer;
        AudioSource audioSource;
        int bufferHead;
        float[] currentBuffer;
        [HideInInspector] public bool midiloaded = false;

        // Start is called before the first frame update
        public void Awake()
        {
            synthesizer = new Synthesizer(sampleRate, channel, bufferSize, 1);
            audioSource = GetComponent<AudioSource>();
            sequencer = new MidiFileSequencer(synthesizer);
            MidiFile midiFile = new MidiFile(midiSource);
            MidiTrack track = midiOnInputPlayback.midiTracks[midiOnInputPlayback.midiTrackNames[midiOnInputPlayback.trackIndex]];
            sequencer.LoadMidiTrack(track, midiFile.BPM, midiFile.Division);
            LoadBank(new PatchBank(bankSource));
            sequencer.Play();
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
                    sequencer.FillMidiEventQueue();
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