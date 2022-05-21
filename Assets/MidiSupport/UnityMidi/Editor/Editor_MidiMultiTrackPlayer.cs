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
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tracks");
        if (player.midiloaded)
        {
            for (int i = 0; i < player.midiTracks.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                player.midiTracks[i].play = EditorGUILayout.Toggle(player.midiTracks[i].play);
                EditorGUILayout.LabelField(player.midiTracks[i].name);                
                player.midiTracks[i].synthIndex = EditorGUILayout.Popup(player.midiTracks[i].synthIndex, player.midiTracks[i].synthPrograms);
                EditorGUILayout.EndHorizontal();
            }

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
