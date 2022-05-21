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
    public class MidiTrackPlayback
    {
        [HideInInspector] public string name;
        public bool play = true;
        [HideInInspector] public MidiTrack track;
        [HideInInspector] public string[] synthPrograms;
        [HideInInspector] public int synthIndex;
    }

    public class MidiMultiTrackPlayer : MonoBehaviour
    {
        [HideInInspector] MidiFileSequencer sequencer;
        [SerializeField] StreamingAssetResouce bankSource;
        [SerializeField] StreamingAssetResouce midiSource;
        [SerializeField] bool playOnAwake = true;
        int channel = 2;
        int sampleRate = 48000;
        int bufferSize = 512;
        [HideInInspector] public List<MidiTrackPlayback> midiTracks = new List<MidiTrackPlayback>();
        Dictionary<string, int> synthPrograms = new Dictionary<string, int>();
        PatchBank bank;
        MidiFile midi;
        Synthesizer synthesizer;
        AudioSource audioSource;
        //MidiFileSequencer sequencer;
        int bufferHead;
        float[] currentBuffer;
        [HideInInspector] public bool midiloaded = false;

        // Start is called before the first frame update
        public void Awake()
        {
            synthesizer = new Synthesizer(sampleRate, channel, bufferSize, 1);
            audioSource = GetComponent<AudioSource>();
            sequencer = new MidiFileSequencer(synthesizer);
            sequencer.LoadMidi(new MidiFile(midiSource));
            LoadBank(new PatchBank(bankSource));

            if (playOnAwake)
            {
                sequencer.Play();
            }

            //Setup playback rules
            for (int i = 0; i < midiTracks.Count; i++)
            {
                sequencer.Synth.SetProgram(i, midiTracks[i].synthIndex);
                
                if (midiTracks[i].play == false)
                {
                    sequencer.SetMute(i, true);
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
                    synthPrograms.Add(patchNumber+1 + "." + bank.GetPatchName(bankNmber, patchNumber), patchNumber);
                    patchNumber++;
                }
                bankNmber++;
            }
            UpdateSynthProgramSelection();
        }
        public void LoadAssociatedMidiIntoTracks()
        {
            MidiFile midi = new MidiFile(midiSource);
            LoadMidiIntoTracks(midi);
        }
        public void LoadMidiIntoTracks(MidiFile midi)
        {
            this.midi = midi;
            midiTracks.Clear();
            foreach (MidiTrack track in midi.Tracks)
            {
                MidiTrackPlayback midiTrackPlayback = new MidiTrackPlayback();
                int count = 0;
                bool hasNotes = false;
                while (count < track.MidiEvents.Length)
                {
                    MidiEvent midiEvent = track.MidiEvents[count];

                    //Name the Track correctly based on the instrument name or Track name Midi Message
                    if (midiEvent.Data1 == 0x03 || midiEvent.Data1 == 0x04)
                    {
                        MetaTextEvent metaTextEvent = (MetaTextEvent)midiEvent;
                        midiTrackPlayback.name = metaTextEvent.Text;
                    }
                    else if (midiEvent.Command == 0x90 || midiEvent.Command == 0x80)
                    {
                        hasNotes = true;
                    }
                    count++;
                }

                if (hasNotes)
                {
                    midiTrackPlayback.track = track;
                    midiTracks.Add(midiTrackPlayback);
                }
            }
            UpdateSynthProgramSelection();
        }
        public void UpdateSynthProgramSelection()
        {
            if(synthPrograms.Count > 0)
            {
                var stringKeys = new string[synthPrograms.Count];
                Dictionary<string, int>.KeyCollection keys = synthPrograms.Keys;
                int i = 0;
                foreach (string key in keys)
                {
                    stringKeys[i] = key;
                    i++;
                }

                for (i = 0; i < midiTracks.Count; i++)
                {
                    midiTracks[i].synthPrograms = stringKeys;
                }
            }

        }
        public void OnEditorLoadMidiClicked()
        {
            LoadAssociatedMidiIntoTracks();
            VisuzlizeBank(new PatchBank(bankSource));
            midiloaded = true;
        }
        public void OnEditorUnloadMidiClicked()
        {
            midiloaded = false;
            midiTracks.Clear();
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

