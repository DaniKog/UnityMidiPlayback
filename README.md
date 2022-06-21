# UnityMidiPlayback
Different Midid Playback methods for Unity

## System Requirements

- Unity 5 Version 2021.3.3f1

## Installation

Download and import the [UnityMidiPlayback Package](https://github.com/DaniKog/Midiplayback/releases/download/UnityMidi/UnityMidiPlaybackV1.0.unitypackage). from Release into your project.
That’s it!

## Features
- Multitrack support
- Support Chords
- Ability to select the synth program
- Play one note on Input

## Playback Methods
Current there are two playback methods supported

### MultiTrack Playback 
Ability to load midi in the editor and select which tracks should play and what synth sound they should use
![image](https://user-images.githubusercontent.com/12103063/174903390-88fb422e-9956-45c2-865b-53096b4091a0.png)

### OnInput Playback
Ability to load midi in the editor and select which track should play and what synth sound they should use
Notes will only play on Input

**SendIndidualEventsOnInput**
- True
  - Sends the next midi event in the list. Note on and Note off sent seperatly.
- False
  - Send the note on and calcualte the next off event and couple them to together.
    Takes into account the BPM nor Divisions for note length. Sending a full note playback

![image](https://user-images.githubusercontent.com/12103063/174904815-924f5c95-2177-485d-9616-05419614da36.png)
