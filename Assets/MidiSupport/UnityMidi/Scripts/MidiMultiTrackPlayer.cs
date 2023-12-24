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
        //Dictionary<string, int> synthPrograms = new Dictionary<string, int>();
        Dictionary<string, Dictionary<string, int>> synthBanks = new Dictionary<string, Dictionary<string, int>>();
        PatchBank bank;
        MidiFile midi;
        Synthesizer synthesizer;
        AudioSource audioSource;
        //MidiFileSequencer sequencer;
        int bufferHead;
        float[] currentBuffer;
        [HideInInspector] public bool midiloaded = false;
        [HideInInspector] public Tuple<bool, int> bankVisualizationDity = new Tuple<bool, int>(false, 0);
        [HideInInspector] public Tuple<bool, int> playbackMuteDity = new Tuple<bool, int>(false, 0);

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

        public void LoadBank(PatchBank bank)
        {
            this.bank = bank;
            synthesizer.UnloadBank();
            synthesizer.LoadBank(bank);
            if (synthBanks.Count == 0)
            {
                VisuzlizeBank(bank);
            }
        }
        public void VisuzlizeBank(PatchBank bank)
        {
            synthBanks.Clear();
            int bankNumber = 0;
            int bankindex = 0;
            while (bank.IsBankLoaded(bankNumber))
            {
                int patchNumber = 0;
                string banktype = "";
                switch (bankindex)
                {
                    case 0: banktype = "Instruments";
                        break;
                    case 1:
                        banktype = "Drums";
                        break;
                    default: banktype = "";
                        break;
                }
                string bankName = bankindex + 1 + "." +banktype;
                synthBanks[bankName] = new Dictionary<string, int>();
                while (bank.GetPatch(bankNumber, patchNumber) != null)
                {
                    synthBanks[bankName].Add(patchNumber+1 + "." + bank.GetPatchName(bankNumber, patchNumber), patchNumber);
                    patchNumber++;
                }
                bankindex++;
                bankNumber = bankNumber + patchNumber; // Bank number is the max index of the previous patch number
            }
            UpdateSynthProgramSelection();
        }
        public void LoadAssociatedMidiIntoTracks()
        {
            MidiFile midi = new MidiFile(midiSource);
            LoadMidiIntoTracks(midi);
        }
        MidiTrack[] GetTracksFromMidi(MidiFile midi)
        {
            if (midi.MidiFormat == TrackFormat.MultiTrack)
            {
                return midi.Tracks;
            }
            else if (midi.MidiFormat == TrackFormat.SingleTrack)
            {
                MidiTrack currentTrack = midi.Tracks[0];
                Dictionary<int, List<MidiEvent>> tracksFromTrack = new Dictionary<int, List<MidiEvent>>();
                foreach (MidiEvent midiEvent in currentTrack.MidiEvents)
                {
                    if (!tracksFromTrack.ContainsKey(midiEvent.Channel))
                    {
                        tracksFromTrack[midiEvent.Channel] = new List<MidiEvent>();
                    }

                    tracksFromTrack[midiEvent.Channel].Add(midiEvent);
                }

                MidiTrack[] tracks = new MidiTrack[tracksFromTrack.Count];
                Dictionary<int, List<MidiEvent>>.KeyCollection keys = tracksFromTrack.Keys;
                
                int i = 0;
                foreach (int key in keys)
                {
                    MidiEvent[] midiEventsToAssign = new MidiEvent[tracksFromTrack[key].Count];
                    int j = 0;
                    foreach (MidiEvent midievent in tracksFromTrack[key])
                    {
                        midiEventsToAssign[j] = midievent;
                        j++;
                    }

                    tracks[i] = new MidiTrack(currentTrack.Instruments, currentTrack.DrumInstruments, midiEventsToAssign);
                    i++;
                }
                Debug.LogWarning("Midi is in a TrackFormat.SingleTrack! Track names and choosing insturment will be disabled ");
                return tracks;
                }

            Debug.LogError("midi is in unsupported forget. Probably MultiSong mode");
            return null;
        }
        public void LoadMidiIntoTracks(MidiFile midi)
        {
            this.midi = midi;
            midiTracks.Clear();
            MidiTrack[] midiTracksToProcess = GetTracksFromMidi(midi);
            foreach (MidiTrack track in midiTracksToProcess)
            {
                MidiTrackPlayback midiTrackPlayback = new MidiTrackPlayback();
                int count = 0;
                bool hasNotes = false;
                while (count < track.MidiEvents.Length)
                {
                    MidiEvent midiEvent = track.MidiEvents[count];

                    //Name the Track correctly based on the instrument name or Track name Midi Message
                    if (midiEvent.Data1 == 0x03)
                    {
                        if (midiEvent.GetType() == typeof(MetaTextEvent))
                        {
                            MetaTextEvent metaTextEvent = (MetaTextEvent)midiEvent;
                            midiTrackPlayback.name = metaTextEvent.Text;
                        }
                    }
                    else if (midiEvent.Command == 0x90 || midiEvent.Command == 0x80)
                    {
                        hasNotes = true;
                    }
                    count++;
                }
                //add name just in case 
                if (midiTrackPlayback.name == null)
                {
                    midiTrackPlayback.name = "Channel." + (track.MidiEvents[0].Channel + 1); //Midi channel is also 0 based
                }

                if (hasNotes)
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
        public void UpdateBankProgramSelection()
        {
            if (synthBanks.Count == 0)
            {
                Debug.LogWarning("Synth Banks visualization not loaded most likely you are editing a Single Track Midi");
                return;
            }
                
            int trackIndex = bankVisualizationDity.Item2;
            midiTracks[trackIndex].synthIndex = 0;
            
            int bankIndex = midiTracks[trackIndex].bankIndex;
            if (bankIndex == 1)
            {
                midiTracks[trackIndex].drumTrack = true;
            }
            else
            {
                midiTracks[trackIndex].drumTrack = false;
            }
            string bankName = midiTracks[trackIndex].banks[bankIndex];

            var stringKeys = new string[synthBanks[bankName].Count];
            Dictionary<string, int>.KeyCollection keys = synthBanks[bankName].Keys;

            int j = 0;
            foreach (string key in keys)
            {
                stringKeys[j] = key;
                j++;
            }
            midiTracks[trackIndex].synthPrograms = stringKeys;
            
            bankVisualizationDity = Tuple.Create(false, 0);
        }
        public void UpdateSynthProgramSelection()
        {
            if (synthBanks.Count > 0)
            {  
                for (int i = 0; i < midiTracks.Count; i++)
                {
                    var bankStringKeys = new string[synthBanks.Count];
                    Dictionary<string, Dictionary<string, int>>.KeyCollection bankKeys = synthBanks.Keys;
                    int j = 0;
                    foreach (string key in bankKeys)
                    {
                        bankStringKeys[j] = key;
                        j++;
                    }
                    midiTracks[i].banks = bankStringKeys;


                    int bankIndex = midiTracks[i].bankIndex;
                    string bankName = midiTracks[i].banks[bankIndex];

                    var stringKeys = new string[synthBanks[bankName].Count];
                    Dictionary<string, int>.KeyCollection keys = synthBanks[bankName].Keys;

                    j = 0;
                    foreach (string key in keys)
                    {
                        stringKeys[j] = key;
                        j++;
                    }
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
            synthBanks.Clear();
        }

        public void OnEditorChangeMade_UpdateBankInstruments()
        {
            //Used mainly to update the visualization of the tracks
            UpdateBankProgramSelection();
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

