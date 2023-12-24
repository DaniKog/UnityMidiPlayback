using UnityEngine;
using System.IO;
using System.Collections;
using AudioSynthesis;
using AudioSynthesis.Bank;
using AudioSynthesis.Synthesis;
using AudioSynthesis.Sequencer;
using AudioSynthesis.Midi;
using System.Collections.Generic;
using AudioSynthesis.Midi.Event;
using static AudioSynthesis.Midi.MidiFile;
using System;

namespace UnityMidi
{
    [RequireComponent(typeof(AudioSource))]

    public class UnityMidiPlayer : MonoBehaviour
    {
        [HideInInspector] protected MidiFileSequencer sequencer;
        [SerializeField] protected StreamingAssetResouce bankSource;
        [SerializeField] protected StreamingAssetResouce midiSource;
        protected int channel = 2;
        protected int sampleRate = 48000;
        protected int bufferSize = 512;
        protected PatchBank bank;
        protected MidiFile midi;
        protected Synthesizer synthesizer;
        protected AudioSource audioSource;
        //MidiFileSequencer sequencer;

        public void SetupSynth()
        {
            synthesizer = new Synthesizer(sampleRate, channel, bufferSize, 1);
            sequencer = new MidiFileSequencer(synthesizer);
            audioSource = GetComponent<AudioSource>();
        }

        public virtual void LoadBank(PatchBank bank)
        {
            this.bank = bank;
            synthesizer.UnloadBank();
            synthesizer.LoadBank(bank);
        }

        public void LoadMidi(MidiFile midi)
        {
            this.midi = midi;
        }

        public MidiTrack[] GetTracksFromMidi(MidiFile midi)
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
        public string GetTrackName(MidiTrack track)
        {
            int count = 0;
            while (count < track.MidiEvents.Length)
            {
                MidiEvent midiEvent = track.MidiEvents[count];

                //Name the Track correctly based on the instrument name or Track name Midi Message
                if (midiEvent.Data1 == 0x03)
                {
                    if (midiEvent.GetType() == typeof(MetaTextEvent))
                    {
                        MetaTextEvent metaTextEvent = (MetaTextEvent)midiEvent;
                        return metaTextEvent.Text;
                    }
                }

                count++;
            }
            //add name just in case 
            if (track.MidiEvents[0] != null)
            {
                return "Channel." + (track.MidiEvents[0].Channel + 1); //Midi channel is also 0 based
            }
            return "Track";
        }
        public bool HasNotes(MidiTrack track)
        {
            int count = 0;
            while (count < track.MidiEvents.Length)
            {
                MidiEvent midiEvent = track.MidiEvents[count];

                if (midiEvent.Command == 0x90 || midiEvent.Command == 0x80)
                {
                    return true;
                }
                count++;
            }
            return false;
        }
    }
}
