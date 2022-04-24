using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
        [HideInInspector] public MidiFileSequencer sequencer;
    }

    public class MidiMultiTrackPlayer : MonoBehaviour
    {
        [SerializeField] StreamingAssetResouce bankSource;
        [SerializeField] StreamingAssetResouce midiSource;
        [SerializeField] bool loadOnAwake = true;
        [SerializeField] bool playOnAwake = true;
        int channel = 2;
        int sampleRate = 48000;
        int bufferSize = 512;
        [SerializeField] List<MidiTrackPlayback> midiTracks = new List<MidiTrackPlayback>();
        PatchBank bank;
        MidiFile midi;
        Synthesizer synthesizer;
        AudioSource audioSource;
        //MidiFileSequencer sequencer;
        int bufferHead;
        float[] currentBuffer;

        // Start is called before the first frame update
        public void Awake()
        {
            synthesizer = new Synthesizer(sampleRate, channel, bufferSize, 1);
            audioSource = GetComponent<AudioSource>();
            LoadBank(new PatchBank(bankSource));

            if (loadOnAwake)
            {
                LoadMidiIntoTracks(new MidiFile(midiSource));
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
        }
        public void PlayTracks()
        {
            //TODO Ability to save the midi file and not reinitilize Midifile on Play
            foreach (MidiTrackPlayback track in midiTracks)
            {
                if (track.play)
                {
                    track.sequencer = new MidiFileSequencer(synthesizer);
                    track.sequencer.Stop();
                    track.sequencer.UnloadMidi();
                    track.sequencer.LoadMidiTrack(track.track, midi.BPM, midi.Division);
                    track.sequencer.Play();
                }
            }
            audioSource.Play();
        }

        void OnAudioFilterRead(float[] data, int channel)
        {
            Debug.Assert(this.channel == channel);
            int count = 0;
            while (count < data.Length)
            {
                if (currentBuffer == null || bufferHead >= currentBuffer.Length)
                {
                    foreach (MidiTrackPlayback track in midiTracks)
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

