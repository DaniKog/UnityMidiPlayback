using UnityEngine;
using UnityEditor;
using UnityMidi;
using System;

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
                GUILayout.FlexibleSpace();
                bool playback = EditorGUILayout.Toggle(player.midiTracks[i].play, GUILayout.Width(15));
                if (player.midiTracks[i].play != playback)
                {
                    player.midiTracks[i].play = playback;
                    player.playbackMuteDity = Tuple.Create(true, i);
                }
                EditorGUILayout.LabelField(player.midiTracks[i].name);
                
                EditorGUILayout.LabelField(player.midiTracks[i].banks[player.midiTracks[i].bankIndex], GUILayout.Width(100));
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
        //Update Changes if needed
        if (GUI.changed)
        {
            if (player.playbackMuteDity.Item1)
            {
                player.OnEditorChangeMade_MuteTrack();
            }
        }
    }
}
