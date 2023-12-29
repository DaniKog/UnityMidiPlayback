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
using static AudioSynthesis.Midi.MidiFile;
using System.Linq;

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
        [HideInInspector] public string[] banks;
        [HideInInspector] public int synthIndex;
        [HideInInspector] public int bankIndex;
        [HideInInspector] public bool drumTrack;
    }

    public class MidiMultiTrackPlayer : UnityMidiPlayer
    {
        [SerializeField] bool playOnAwake = true;

        [HideInInspector] public List<MidiTrackPlayback> midiTracks = new List<MidiTrackPlayback>();
        //MidiFileSequencer sequencer;
        [HideInInspector] public bool midiloaded = false;
        [HideInInspector] public Tuple<bool, int> playbackMuteDity = new Tuple<bool, int>(false, 0);

        int bufferHead;
        float[] currentBuffer;
        // Start is called before the first frame update
        public void Awake()
        {
            SetupSynth(); 
            sequencer.LoadMidi(new MidiFile(midiSource));
            LoadBank(new PatchBank(bankSource));

            if (playOnAwake)
            {
                sequencer.Play();
            }

            //Setup playback rules
            for (int i = 0; i < midiTracks.Count; i++)
            {
                if (midiTracks[i].drumTrack == true)
                {
                    sequencer.Synth.SetDrumChannel(i);
                }

                if (midiTracks[i].play == false)
                {
                    sequencer.SetMute(i, true);
                }

                sequencer.Synth.SetProgram(i, midiTracks[i].synthIndex);
            }
        }

        public override void LoadBank(PatchBank bank)
        {
            base.LoadBank(bank);
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
            MidiTrack[] midiTracksToProcess = GetTracksFromMidi(midi);
            foreach (MidiTrack track in midiTracksToProcess)
            {
                MidiTrackPlayback midiTrackPlayback = new MidiTrackPlayback();
                midiTrackPlayback.name = GetTrackName(track);

                if (HasNotes(track))
                {
                    midiTrackPlayback.track = track;
                    if (track.Instruments.Length > 0)
                    {
                        midiTrackPlayback.synthIndex = track.Instruments[0];
                    }
                    else if(track.DrumInstruments.Length > 0)
                    {
                        // Usually Drum bank is 2nd bank
                        midiTrackPlayback.bankIndex = 1;
                        midiTrackPlayback.synthIndex = track.DrumInstruments[0];
                        midiTrackPlayback.drumTrack = true;
                    }
                    midiTracks.Add(midiTrackPlayback);
                }
            }
            UpdateSynthProgramSelection();
        }
        public void UpdateSynthProgramSelection()
        {
            for (int i = 0; i < midiTracks.Count; i++)
            {

                midiTracks[i].banks = GetBankStringKeys();

                int bankIndex = midiTracks[i].bankIndex;
                string bankName = midiTracks[i].banks[bankIndex];

                midiTracks[i].synthPrograms = GetSynthStringKeys(bankName);
            }

        }
        public void OnEditorLoadMidiClicked()
        {
            VisuzlizeBank(new PatchBank(bankSource));
            LoadAssociatedMidiIntoTracks();
            midiloaded = true;
        }
        public void OnEditorUnloadMidiClicked()
        {
            midiloaded = false;
            midiTracks.Clear();
            ClearSynthBanks();
        }
        public void OnEditorChangeMade_MuteTrack()
        {
            int trackIndex = playbackMuteDity.Item2;
            MuteTrack(trackIndex, !midiTracks[trackIndex].play);
            playbackMuteDity = Tuple.Create(false, 0);
        }
        public void MuteTrack(int trackNumber, bool mute)
        {
            if(sequencer != null)
            {
                sequencer.SetMute(trackNumber, mute);
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

