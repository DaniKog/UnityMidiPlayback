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
                bool playback = EditorGUILayout.Toggle(player.midiTracks[i].play);
                if (player.midiTracks[i].play != playback)
                {
                    player.midiTracks[i].play = playback;
                    player.playbackMuteDity = Tuple.Create(true, i);
                }
                EditorGUILayout.LabelField(player.midiTracks[i].name);               
                int bankIndex = EditorGUILayout.Popup(player.midiTracks[i].bankIndex, player.midiTracks[i].banks);
                if (player.midiTracks[i].bankIndex != bankIndex)
                {
                    player.midiTracks[i].bankIndex = bankIndex;
                    player.bankVisualizationDity = Tuple.Create(true, i);
                }
                    
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
            if(player.bankVisualizationDity.Item1)
            {
                player.OnEditorChangeMade_UpdateBankInstruments();
            }
            if (player.playbackMuteDity.Item1)
            {
                player.OnEditorChangeMade_MuteTrack();
            }
        }
    }
}
