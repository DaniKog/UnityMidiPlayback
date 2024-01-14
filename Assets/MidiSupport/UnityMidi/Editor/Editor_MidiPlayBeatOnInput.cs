using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityMidi;

[CustomEditor(typeof(MidiPlayBeatOnInput))]
public class Editor_MidiPlayBeatOnInput : Editor_MidiMultiTrackPlayer
{
    // Update is called once per frame
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
    }
}