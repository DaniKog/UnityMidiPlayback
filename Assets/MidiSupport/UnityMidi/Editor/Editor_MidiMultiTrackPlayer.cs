using UnityEngine;
using UnityEditor;
using UnityMidi;

[CustomEditor(typeof(MidiMultiTrackPlayer))]

public class Editor_MidiMultiTrackPlayer : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        MidiMultiTrackPlayer player = (MidiMultiTrackPlayer)target;

        if (GUILayout.Button("Load Midi"))
        {
            player.LoadAssociatedMidiIntoTracks();
        }
    }
}
