using UnityEngine;
using UnityEditor;
using UnityMidi;

[CustomEditor(typeof(MidiMultiChannelPlayer))]
public class Editor_MuiltiChannelMidi : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        MidiMultiChannelPlayer player = (MidiMultiChannelPlayer)target;

        if (GUILayout.Button("Load Midi"))
        {
            player.LoadAssociatedMidiIntoTracks();
        }
    }

}