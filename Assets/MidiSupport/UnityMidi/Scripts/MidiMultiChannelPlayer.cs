using UnityEngine;
using System.IO;
using System.Collections;
using AudioSynthesis;
using AudioSynthesis.Bank;
using AudioSynthesis.Synthesis;
using AudioSynthesis.Sequencer;
using AudioSynthesis.Midi;
using AudioSynthesis.Midi.Event;
using System.Collections.Generic;

namespace UnityMidi
{
    [RequireComponent(typeof(AudioSource))]

    [System.Serializable]
    public class MidiTrackPlaybackSettings
    {
        [HideInInspector] public string name;
        public bool play = true;
        [HideInInspector] public MidiTrack track;
        [HideInInspector] public MidiFileSequencer sequencer;
    }

    [System.Serializable]
    public class VisualMidiTrack
    {
        [HideInInspector] public string name;
        public List<VisualMidiEvent> m_list;
    }

    [System.Serializable]
    public class VisualMidiEvent
    {
        public VisualMidiEvent(string eventName, MidiEvent midiEvent, bool isNoteOnOff, string noteKey)
        {
            name = eventName;
            m_MidiEvent = midiEvent;
            m_NoteOnOff = isNoteOnOff;
            m_NoteKey = noteKey;
        }
        string name;
        [SerializeField] string m_NoteKey;
        [SerializeField] bool m_NoteOnOff = false;
        [SerializeField] MidiEvent m_MidiEvent;
    }
    public class MidiMultiChannelPlayer : MonoBehaviour
    {
        [SerializeField] StreamingAssetResouce bankSource;
        [SerializeField] StreamingAssetResouce midiSource;
        [SerializeField] bool loadOnAwake = true;
        [SerializeField] bool playOnAwake = true;
        [SerializeField] int channel = 2;
        [SerializeField] int sampleRate = 48000;
        [SerializeField] int bufferSize = 1024;
        [SerializeField] List<VisualMidiTrack> midiVisualTracks = new List<VisualMidiTrack>();
        [SerializeField] List<MidiTrackPlaybackSettings> midiTracks = new List<MidiTrackPlaybackSettings>();
        PatchBank bank;
        MidiFile midi;
        Synthesizer synthesizer;
        AudioSource audioSource;
        //MidiFileSequencer sequencer;
        int bufferHead;
        float[] currentBuffer;

        public AudioSource AudioSource { get { return audioSource; } }

        //public MidiFileSequencer Sequencer { get { return sequencer; } }

        public PatchBank Bank { get { return bank; } }

        public MidiFile MidiFile { get { return midi; } }

        public void Awake()
        {
            synthesizer = new Synthesizer(sampleRate, channel, bufferSize, 1);
            //sequencer = new MidiFileSequencer(synthesizer);
           //print(sequencer.Synth.midiEventQueue);
            audioSource = GetComponent<AudioSource>();
            if (loadOnAwake)
            {
                LoadBank(new PatchBank(bankSource));
                LoadMidi(new MidiFile(midiSource));
            }

            
            if (playOnAwake)
            {
                PlayTracks();
            }
        }

        public void LoadBank(PatchBank bank)
        {
            this.bank = bank;
            synthesizer.UnloadBank();
            synthesizer.LoadBank(bank);
        }
        public void LoadAssociatedMidiIntoTracks()
        {
            MidiFile midi = new MidiFile(midiSource);
            LoadMidiIntoTracks(midi);
        }
        // TODO
        public MidiTrack GetMidiTrackByName(MidiFile midi, string Name)
        {
            foreach (MidiTrack track in midi.Tracks)
            {
                int count = 0;
                while (count < track.MidiEvents.Length)
                {
                    MidiEvent midiEvent = track.MidiEvents[count];

                    //Name the Track correctly based on the instrument name or Track name Midi Message
                    if (midiEvent.Data1 == 0x03 || midiEvent.Data1 == 0x04)
                    {
                        MetaTextEvent metaTextEvent = (MetaTextEvent)midiEvent;
                        if(Name == metaTextEvent.Text)
                        {
                            return track;
                        }
                    }
                    count++;
                }
            }
            return null;
        }
        //TODO Fix Storing midi file info in the list as it is reset on play
        public void LoadMidiIntoTracks(MidiFile midi)
        {
            midiTracks.Clear();
            foreach (MidiTrack track in midi.Tracks)
            {
                MidiTrackPlaybackSettings midiTrackSetting = new MidiTrackPlaybackSettings();
                int count = 0;
                bool hasNotes = false;
                while (count < track.MidiEvents.Length)
                {
                    MidiEvent midiEvent = track.MidiEvents[count];

                    //Name the Track correctly based on the instrument name or Track name Midi Message
                    if (midiEvent.Data1 == 0x03 || midiEvent.Data1 == 0x04)
                    {
                        MetaTextEvent metaTextEvent = (MetaTextEvent)midiEvent;
                        midiTrackSetting.name = metaTextEvent.Text;
                    }
                    else if (midiEvent.Command == 0x90 || midiEvent.Command == 0x80)
                    {
                        hasNotes = true;
                    }
                    count++;
                }

                if(hasNotes)
                {
                    midiTrackSetting.track = track;
                    midiTracks.Add(midiTrackSetting);
                }
            }
        }

        public void VisualizeMidi(MidiFile midi)
        {
            midiVisualTracks.Clear();

            foreach (MidiTrack track in midi.Tracks)
            {
                int count = 0;
                VisualMidiTrack visualTrack = new VisualMidiTrack();
                visualTrack.m_list = new List<VisualMidiEvent>();
                while (count < track.MidiEvents.Length)
                {
                    MidiEvent midiEvent = track.MidiEvents[count];

                    //Name the Track correctly based on the instrument name or Track name Midi Message
                    if (midiEvent.Data1 == 0x03 || midiEvent.Data1 == 0x04)
                    {
                        MetaTextEvent metaTextEvent = (MetaTextEvent)midiEvent;
                        visualTrack.name = metaTextEvent.Text;
                    }
                    //Check for note on Command
                    else if (midiEvent.Command == 0x90)
                    {
                        VisualMidiEvent visualMidiEvent = new VisualMidiEvent(midiEvent.ToString(), midiEvent, true, MidiFile.getNoteName(midiEvent.Data1));
                        visualTrack.m_list.Add(visualMidiEvent);
                    }
                    //Check for note off Command
                    else if (midiEvent.Command == 0x80)
                    {
                        VisualMidiEvent visualMidiEvent = new VisualMidiEvent(midiEvent.ToString(), midiEvent, false, MidiFile.getNoteName(midiEvent.Data1));
                        visualTrack.m_list.Add(visualMidiEvent);
                    }
                    count++;
                }

                //Only add a track if it has note messages in it
                if (visualTrack.m_list.Count > 0)
                    this.midiVisualTracks.Add(visualTrack);
            }
        }
        public void VisualizeAssociateMidi()
        {
            MidiFile midi = new MidiFile(midiSource);
            VisualizeMidi(midi);
        }

        public void LoadMidi(MidiFile midi)
        {
            this.midi = midi;
            VisualizeMidi(midi);
            //sequencer.Stop();
            //sequencer.UnloadMidi();
            //sequencer.LoadMidi(midi);
        }


        public void Play()
        {
            //sequencer.Play();
            //audioSource.Play();
        }
        public void PlayTracks()
        {
            //TODO Ability to save the midi file and not reinitilize Midifile on Play
            foreach (MidiTrackPlaybackSettings track in midiTracks)
            {
                if (track.play)
                {
                    track.sequencer = new MidiFileSequencer(synthesizer);
                    track.sequencer.Stop();
                    track.sequencer.UnloadMidi();
                    if(track.track == null)
                    {
                        track.track = GetMidiTrackByName(MidiFile, track.name) ;
                    }
                    track.sequencer.LoadMidiTrack(track.track, midi.BPM, midi.Division);
                    track.sequencer.Play();
                    audioSource.Play();
                }
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
                    foreach (MidiTrackPlaybackSettings track in midiTracks)
                    {
                        if (track.play)
                        {
                            track.sequencer.FillMidiEventQueue();
                        }
                    }
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
