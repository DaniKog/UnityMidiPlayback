using UnityEngine;
using UnityEditor;
using UnityMidi;

[CustomEditor(typeof(MidiPlayOnInput))]

public class Editor_MidiPlayOnInput : Editor
{

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        MidiPlayOnInput player = (MidiPlayOnInput)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Track playback");
        if (player.midiloaded)
        {
            EditorGUILayout.BeginHorizontal();
            player.midiOnInputPlayback.trackIndex = EditorGUILayout.Popup(player.midiOnInputPlayback.trackIndex, player.midiOnInputPlayback.midiTrackNames);                
            player.midiOnInputPlayback.synthIndex = EditorGUILayout.Popup(player.midiOnInputPlayback.synthIndex, player.midiOnInputPlayback.synthPrograms);                
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            if (GUILayout.Button("Reload Midi"))
            {
                player.OnEditorLoadMidiClicked();
            }
        }
        else
        {
            if (GUILayout.Button("Load Midi"))
            {
                player.OnEditorLoadMidiClicked();
            }
        }

        if (GUILayout.Button("Unload Midi"))
        {
            player.OnEditorUnloadMidiClicked();
        }
    }
}
