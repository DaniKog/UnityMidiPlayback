using UnityEngine;
using UnityEditor;
using UnityMidi;

[CustomEditor(typeof(MidiPlayOnInput))]

public class Editor_MidiPlayOnInput : Editor
{
    MidiPlayOnInput player;
    private void Awake()
    {
        player = (MidiPlayOnInput)target;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Track playback");
        if (player.midiloaded)
        {
            EditorGUILayout.BeginHorizontal();
            int trackIndex = EditorGUILayout.Popup(player.midiOnInputPlayback.trackIndex, player.midiOnInputPlayback.midiTrackNames);
            if (player.midiOnInputPlayback.trackIndex != trackIndex)
            {
                player.midiOnInputPlayback.trackIndex = trackIndex;
                player.OnEditorChangeMade_TrackIndex();
            }
            EditorGUILayout.LabelField(player.midiOnInputPlayback.banks[player.midiOnInputPlayback.bankIndex], GUILayout.Width(100));
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
