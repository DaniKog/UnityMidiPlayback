using AudioSynthesis.Bank;
using AudioSynthesis.Midi;
using AudioSynthesis.Midi.Event;
using AudioSynthesis.Sequencer;
using AudioSynthesis.Synthesis;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityMidi;

public class MidiPlayBeatOnInput : MidiMultiTrackPlayer
{
    bool endOfTrackReached = false;
    List<MidiTrack> utilityTrack = new List<MidiTrack>();
    double sampleTimeInterval = 10000;
    double timeToStop = 0;
    public bool fadeAfterBeat = false;
    public float fadeOutSpeed = 5.0f;
    public float fadeInSpeed = 50.0f;
    bool fadein = false;
    bool fadeout = false;
    bool initilized = false;
    // Start is called before the first frame update
    public enum Rate { ThrirityTwo, Sixteenth, Eighth ,Quater, Half, Bar }
    public Rate rate = Rate.Quater;
    public override void Awake() 
    {
        SetupSynth();
        LoadMidi(new MidiFile(midiSource));
        LoadBank(new PatchBank(bankSource));
        CalculateBeatSampleInterval(midi.BPM);
        sequencer.LoadMidi(midi);
        SetCurrentTimeToFirstNoteFromSynth(sequencer);
        SetupSequnecer();
        sequencer.Play(); // This plays all the notes up to the 1st note
        initilized = false;
    }
    // Update is called once per frame
    public void Update()
    {
        if (fadein)
        {
            if (audioSource.volume < 1)
            {
                audioSource.volume += audioSource.volume * Time.deltaTime * fadeInSpeed;
            }
            else
            {
                fadein = false;
            }
        }

        if (fadeout)
        {
            if (audioSource.volume >= 0)
            {
                audioSource.volume -= audioSource.volume * Time.deltaTime * fadeOutSpeed;
            }
            else
            {
                fadeout = false;
            }
        }

        if (Input.GetKeyDown("space") || Input.GetMouseButtonDown(0))
        {
            if (!endOfTrackReached && initilized)
            {
                sequencer.Play();
                timeToStop += sampleTimeInterval;
                if (fadeAfterBeat)
                {
                    fadein = true;
                    fadeout = false;
                }
            }
        }
        if (sequencer.IsPlaying)
        {
            if (sequencer.CurrentTime >= timeToStop)
            {
                sequencer.Pause();
                if (!initilized)
                {
                    Debug.Log("Play Arrow is on First Notes. Ready To Play");
                    initilized = true;
                }
                if (fadeAfterBeat)
                {
                   fadeout = true;
                }
            }
        }


    }
    public void CalculateBeatSampleInterval(double BPM)
    {
        sampleTimeInterval = sampleRate * (60.0 / BPM);
        switch (rate)
        {
            case Rate.ThrirityTwo:
                sampleTimeInterval = sampleTimeInterval / 8;
                break;
            case Rate.Sixteenth:
                sampleTimeInterval = sampleTimeInterval / 4;
                break;
            case Rate.Eighth:
                sampleTimeInterval = sampleTimeInterval / 2;
                break;
            case Rate.Quater:
                break;
            case Rate.Half:
                sampleTimeInterval = sampleTimeInterval * 2;
                break;
            case Rate.Bar:
                sampleTimeInterval = sampleTimeInterval * 4;
                break;
            default: break;
        }
    }
    public void SetCurrentTimeToFirstNoteFromSynth(MidiFileSequencer synth)
    {
        MidiMessage[] midiMessages = synth.Data;
        foreach (MidiMessage midiMessage in midiMessages)
        {
            if (midiMessage.command == 0x90)
            {
                // Stop 1 buffersize before the 1st note
                timeToStop = midiMessage.delta - bufferSize;
                return;
            }
        }
    }

    public void SetCurrentTimeToFirstNoteFromFile(MidiFile midifile)
    {
        //Note that Delta time needs to be recacluated based on Sample rate which already done in the synth
        int shortestTimeToFirstNote = -1;
        if (midifile.MidiFormat == MidiFile.TrackFormat.SingleTrack)
        {
            shortestTimeToFirstNote = GetFirstNoteTimeFromTrack(midifile.Tracks[0]);
        }
        if (midifile.MidiFormat == MidiFile.TrackFormat.MultiTrack)
        {
            int trackIndex = 0;
            MidiTrack[] midiTracksToProcess = GetTracksFromMidi(midifile);
            int[] timesToFirstNote = new int[midiTracksToProcess.Length];
            foreach (MidiTrack track in midiTracksToProcess)
            {
                timesToFirstNote[trackIndex] = GetFirstNoteTimeFromTrack(track);
                trackIndex++;
            }
            
            foreach (int timeToFirstNote in timesToFirstNote)
            {
                if (timeToFirstNote >= 0)
                {
                    if (shortestTimeToFirstNote < 0)
                    {
                        shortestTimeToFirstNote = timeToFirstNote;
                    }
                    else if (shortestTimeToFirstNote > timeToFirstNote)
                    {
                        shortestTimeToFirstNote = timeToFirstNote;
                    }
                }
            }
        }

        if (shortestTimeToFirstNote < 0)
        {
            shortestTimeToFirstNote = 0;
        }
        // Stop 1 buffersize before the 1st note
        timeToStop = shortestTimeToFirstNote * sampleRate * (60.0 / (midifile.BPM * midifile.Division)) - bufferSize; 
    }
    int GetFirstNoteTimeFromTrack(MidiTrack track)
    {
        int count = 0;
        int firstNoteTime = 0;
        while (count < track.MidiEvents.Length)
        {
            MidiEvent midiEvent = track.MidiEvents[count];
            firstNoteTime += midiEvent.DeltaTime;
            if (midiEvent.Command == 0x90)
            {
                Debug.Log(GetTrackName(track) + " : " + midiEvent.ToString() + " " + MidiFile.getNoteName(midiEvent.Data1) + " : " + firstNoteTime);
                return firstNoteTime;
            }
            count++;
        }
        return -1;
    }
    void OnAudioFilterRead(float[] data, int channel)
    {
        Debug.Assert(this.channel == channel);
        int count = 0;
        while (count < data.Length)
        {
            if (currentBuffer == null || bufferHead >= currentBuffer.Length)
            {
                sequencer.FillMidiEventQueueTillPause(timeToStop);
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

