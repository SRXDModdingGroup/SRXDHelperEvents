using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SMU.Utilities;

namespace SRXDHelperEvents {
    public static class NoteEvents {
        public class NoteHitEventArgs {
            public int NoteIndex { get; }
            
            public Note Note { get; }
            
            public float TimeOffset { get; }

            internal NoteHitEventArgs(int noteIndex, Note note, float timeOffset) {
                NoteIndex = noteIndex;
                Note = note;
                TimeOffset = timeOffset;
            }
        }
        
        public class NoteMissEventArgs {
            public int NoteIndex { get; }
            
            public int EndNoteIndex { get; }
            
            public Note Note { get; }
            
            public Note EndNote { get; }

            internal NoteMissEventArgs(int noteIndex, int endNoteIndex, Note note, Note endNote) {
                NoteIndex = noteIndex;
                EndNoteIndex = endNoteIndex;
                Note = note;
                EndNote = endNote;
            }
        }
        
        public static bool Playing { get; private set; }
        
        public static event Action<NoteHitEventArgs> OnNoteHit;

        public static event Action<NoteHitEventArgs> OnSustainedNoteTick;

        public static event Action OnOverbeat;

        public static event Action<NoteMissEventArgs> OnNoteMiss;

        public static event Action<NoteMissEventArgs> OnSustainedNoteMiss;

        private static float lastTimeOffset;
        private static PlayableNoteData noteData;

        private static void Overbeat() {
            if (Playing)
                OnOverbeat?.Invoke();
        }

        private static void NoteMiss(int noteIndex, Note note) {
            if (Playing)
                OnNoteMiss?.Invoke(new NoteMissEventArgs(noteIndex, -1, note, new Note()));
        }

        private static void BeatMiss(int noteIndex, Note note) {
            if (!Playing)
                return;
            
            if (note.IsDrumHitExtension) {
                int endNoteIndex = note.endNoteIndex;
                
                OnNoteMiss?.Invoke(new NoteMissEventArgs(noteIndex, endNoteIndex, note, noteData.GetNote(endNoteIndex)));
            }
            else
                OnNoteMiss?.Invoke(new NoteMissEventArgs(noteIndex, -1, note, new Note()));
        }
        
        private static void BeatHoldMiss(int noteIndex, int endNoteIndex, Note note, Note endNote) {
            if (Playing)
                OnSustainedNoteMiss?.Invoke(new NoteMissEventArgs(noteIndex, endNoteIndex, note, endNote));
        }

        private static void HoldMiss(int noteIndex, int endNoteIndex, bool hasEntered) {
            if (!Playing)
                return;
            
            if (hasEntered)
                OnSustainedNoteMiss?.Invoke(new NoteMissEventArgs(noteIndex, endNoteIndex, noteData.GetNote(noteIndex), noteData.GetNote(endNoteIndex)));
            else
                OnNoteMiss?.Invoke(new NoteMissEventArgs(noteIndex, endNoteIndex, noteData.GetNote(noteIndex), noteData.GetNote(endNoteIndex)));
        }

        private static void LiftoffMiss(int noteIndex) {
            if (Playing)
                OnNoteMiss?.Invoke(new NoteMissEventArgs(noteIndex, -1, noteData.GetNote(noteIndex), new Note()));
        }

        private static void SpinMiss(int noteIndex, Note note, bool failedInitialSpin) {
            if (!Playing)
                return;
            
            if (failedInitialSpin)
                OnNoteMiss?.Invoke(new NoteMissEventArgs(noteIndex, -1, note, new Note()));
            else
                OnSustainedNoteMiss?.Invoke(new NoteMissEventArgs(noteIndex, -1, note, new Note()));
        }

        private static void ScratchMiss(int noteIndex) {
            if (Playing)
                OnSustainedNoteMiss?.Invoke(new NoteMissEventArgs(noteIndex, -1, noteData.GetNote(noteIndex), new Note()));
        }

        [HarmonyPatch(typeof(Track), nameof(Track.PlayTrack)), HarmonyPostfix]
        private static void Track_PlayTrack_Postfix(Track __instance) {
            noteData = __instance.playStateFirst.trackData.NoteData;
            Playing = true;
        }
        
        [HarmonyPatch(typeof(Track), nameof(Track.PracticeTrack)), HarmonyPostfix]
        private static void Track_PracticeTrack_Postfix(Track __instance) {
            noteData = __instance.playStateFirst.trackData.NoteData;
            Playing = true;
        }

        [HarmonyPatch(typeof(Track), nameof(Track.ReturnToPickTrack)), HarmonyPostfix]
        private static void Track_ReturnToPickTrack_Postfix() {
            Playing = false;
        }

        [HarmonyPatch(typeof(PlayState), nameof(PlayState.Complete))]
        private static void PlayState_Complete_Postfix() {
            Playing = false;
        }

        [HarmonyPatch(typeof(GameplayVariables), nameof(GameplayVariables.GetTimingAccuracy)), HarmonyPostfix]
        private static void GameplayVariables_GetTimingAccuracy_Postfix(float timeOffset) {
            if (Playing)
                lastTimeOffset = timeOffset;
        }
        
        [HarmonyPatch(typeof(GameplayVariables), nameof(GameplayVariables.GetTimingAccuracyForBeat)), HarmonyPostfix]
        private static void GameplayVariables_GetTimingAccuracyForBeat_Postfix(float timeOffset) {
            if (Playing)
                lastTimeOffset = timeOffset;
        }

        [HarmonyPatch(typeof(TrackGameplayLogic), "AddScoreIfPossible"), HarmonyPostfix]
        private static void TrackGameplayLogic_AddScoreIfPossible_Postfix(int pointsToAdd, int noteIndex) {
            if (!Playing)
                return;
            
            var note = noteData.GetNote(noteIndex);
            
            if (pointsToAdd == 1)
                OnSustainedNoteTick?.Invoke(new NoteHitEventArgs(noteIndex, note, 0f));
            else {
                var noteType = note.NoteType;

                if (noteType != NoteType.Tap && noteType != NoteType.HoldStart && noteType != NoteType.DrumStart && noteType != NoteType.SectionContinuationOrEnd
                    && (noteType != NoteType.DrumEnd || note.DrumEndType != DrumSection.EndType.Release))
                    lastTimeOffset = 0f;
                
                OnNoteHit?.Invoke(new NoteHitEventArgs(noteIndex, note, lastTimeOffset));
            }
        }

        [HarmonyPatch(typeof(Track), nameof(Track.Update)), HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Track_Update_Transpiler(IEnumerable<CodeInstruction> instructions) {
            var instructionsList = new List<CodeInstruction>(instructions);
            var NoteEvents_Overbeat = typeof(NoteEvents).GetMethod(nameof(Overbeat), BindingFlags.NonPublic | BindingFlags.Static);
            var ScoreState_DropMultiplier = typeof(PlayState.ScoreState).GetMethod(nameof(PlayState.ScoreState.DropMultiplier));

            var match = PatternMatching.Match(instructionsList, new Func<CodeInstruction, bool>[] {
                instr => instr.Calls(ScoreState_DropMultiplier)
            }).First()[0];
            
            instructionsList.Insert(match.End, new CodeInstruction(OpCodes.Call, NoteEvents_Overbeat));

            return instructionsList;
        }
        
        [HarmonyPatch(typeof(TrackGameplayLogic), nameof(TrackGameplayLogic.UpdateNoteState)), HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TrackGameplayLogic_UpdateNoteState_Transpiler(IEnumerable<CodeInstruction> instructions) {
            var instructionsList = new List<CodeInstruction>(instructions);
            var insertions = new DeferredInsertion<CodeInstruction>();
            var NoteEvents_NoteMiss = typeof(NoteEvents).GetMethod(nameof(NoteMiss), BindingFlags.NonPublic | BindingFlags.Static);
            var NoteEvents_BeatMiss = typeof(NoteEvents).GetMethod(nameof(BeatMiss), BindingFlags.NonPublic | BindingFlags.Static);
            var NoteEvents_BeatHoldMiss = typeof(NoteEvents).GetMethod(nameof(BeatHoldMiss), BindingFlags.NonPublic | BindingFlags.Static);
            var ScoreState_DropMultiplier = typeof(PlayState.ScoreState).GetMethod(nameof(PlayState.ScoreState.DropMultiplier));
            var TrackGameplayLogic_AllowErrorToOccur = typeof(TrackGameplayLogic).GetMethod(nameof(TrackGameplayLogic.AllowErrorToOccur));
            var Note_endNoteIndex = typeof(Note).GetField(nameof(Note.endNoteIndex));

            var matches = PatternMatching.Match(instructionsList, new Func<CodeInstruction, bool>[] {
                instr => instr.Calls(ScoreState_DropMultiplier)
            });

            foreach (var match in matches) {
                var result = match[0];

                if (instructionsList[result.Start - 4].Branches(out _)) {
                    insertions.Add(result.End, new CodeInstruction[] {
                        new (OpCodes.Ldloc_1), // note
                        new (OpCodes.Ldfld, Note_endNoteIndex),
                        new (OpCodes.Ldloc_S, (byte) 42), // endNote
                        new (OpCodes.Call, NoteEvents_NoteMiss)
                    });
                }
                else {
                    insertions.Add(result.End, new CodeInstruction[] {
                        new (OpCodes.Ldarg_2), // noteIndex
                        new (OpCodes.Ldloc_1), // note
                        new (OpCodes.Ldfld, Note_endNoteIndex),
                        new (OpCodes.Ldloc_1), // note
                        new (OpCodes.Ldloc_S, (byte) 42), // endNote
                        new (OpCodes.Call, NoteEvents_BeatHoldMiss)
                    });
                }
            }
            
            matches = PatternMatching.Match(instructionsList, new Func<CodeInstruction, bool>[] {
                instr => instr.Calls(TrackGameplayLogic_AllowErrorToOccur),
                instr => instr.opcode == OpCodes.Pop
            });

            foreach (var match in matches) {
                insertions.Add(match[0].End, new CodeInstruction[] {
                    new (OpCodes.Ldarg_2), // noteIndex
                    new (OpCodes.Ldloc_1), // note
                    new (OpCodes.Call, NoteEvents_BeatMiss)
                });
            }
            
            insertions.Insert(instructionsList);

            return instructionsList;
        }

        [HarmonyPatch(typeof(TrackGameplayLogic), nameof(TrackGameplayLogic.UpdateFreestyleSectionState)), HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TrackGameplayLogic_UpdateFreestyleSectionState_Transpiler(IEnumerable<CodeInstruction> instructions) {
            var instructionsList = new List<CodeInstruction>(instructions);
            var insertions = new DeferredInsertion<CodeInstruction>();
            var NoteEvents_HoldMiss = typeof(NoteEvents).GetMethod(nameof(HoldMiss), BindingFlags.NonPublic | BindingFlags.Static);
            var NoteEvents_LiftoffMiss = typeof(NoteEvents).GetMethod(nameof(LiftoffMiss), BindingFlags.NonPublic | BindingFlags.Static);
            var TrackGameplayLogic_AllowErrorToOccur = typeof(TrackGameplayLogic).GetMethod(nameof(TrackGameplayLogic.AllowErrorToOccur));
            var FreestyleSection_firstNoteIndex = typeof(FreestyleSection).GetField(nameof(FreestyleSection.firstNoteIndex));
            var FreestyleSection_endNoteIndex = typeof(FreestyleSection).GetField(nameof(FreestyleSection.endNoteIndex));
            var FreestyleSectionState_hasEntered = typeof(FreestyleSectionState).GetField(nameof(FreestyleSectionState.hasEntered));
            var FreestyleSectionState_releaseState = typeof(FreestyleSectionState).GetField(nameof(FreestyleSectionState.releaseState));
            
            var match = PatternMatching.Match(instructionsList, new Func<CodeInstruction, bool>[] {
                instr => instr.Calls(TrackGameplayLogic_AllowErrorToOccur),
                instr => instr.opcode == OpCodes.Pop
            }).Then(new Func<CodeInstruction, bool>[] {
                instr => instr.labels.Count > 0
            }).First()[1];
            
            insertions.Add(match.End, new CodeInstruction[] {
                new (OpCodes.Ldloc_S, (byte) 6), // section
                new (OpCodes.Ldfld, FreestyleSection_firstNoteIndex),
                new (OpCodes.Ldloc_S, (byte) 6), // section
                new (OpCodes.Ldfld, FreestyleSection_endNoteIndex),
                new (OpCodes.Ldarg_S, (byte) 4), // state
                new (OpCodes.Ldfld, FreestyleSectionState_hasEntered),
                new (OpCodes.Call, NoteEvents_HoldMiss)
            });

            match = PatternMatching.Match(instructionsList, new Func<CodeInstruction, bool>[] {
                instr => instr.IsLdarg(), // state
                instr => instr.opcode == OpCodes.Ldc_I4_5, // ReleaseState.Failed
                instr => instr.StoresField(FreestyleSectionState_releaseState)
            }).First()[0];
            
            insertions.Add(match.End, new CodeInstruction[] {
                new (OpCodes.Ldloc_S, (byte) 6), // section
                new (OpCodes.Ldfld, FreestyleSection_endNoteIndex),
                new (OpCodes.Call, NoteEvents_LiftoffMiss)
            });
            
            insertions.Insert(instructionsList);

            return instructionsList;
        }

        [HarmonyPatch(typeof(TrackGameplayLogic), nameof(TrackGameplayLogic.UpdateSpinSectionState)), HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TrackGameplayLogic_UpdateSpinSectionState_Transpiler(IEnumerable<CodeInstruction> instructions) {
            var instructionsList = new List<CodeInstruction>(instructions);
            var NoteEvents_SpinMiss = typeof(NoteEvents).GetMethod(nameof(SpinMiss), BindingFlags.NonPublic | BindingFlags.Static);
            var TrackGameplayLogic_AllowErrorToOccur = typeof(TrackGameplayLogic).GetMethod(nameof(TrackGameplayLogic.AllowErrorToOccur));
            var SpinnerSection_noteIndex = typeof(ScratchSection).GetField(nameof(SpinnerSection.noteIndex));
            var SpinSectionState_failedInitialSpin = typeof(SpinSectionState).GetField(nameof(SpinSectionState.failedInitialSpin));
            
            var match = PatternMatching.Match(instructionsList, new Func<CodeInstruction, bool>[] {
                instr => instr.Calls(TrackGameplayLogic_AllowErrorToOccur),
                instr => instr.opcode == OpCodes.Pop
            }).First()[0];

            instructionsList.InsertRange(match.End, new CodeInstruction[] {
                new (OpCodes.Ldloc_3), // section
                new (OpCodes.Ldfld, SpinnerSection_noteIndex),
                new (OpCodes.Ldloc_S, (byte) 4), // note
                new (OpCodes.Ldarg_S, (byte) 4), // state
                new (OpCodes.Ldfld, SpinSectionState_failedInitialSpin),
                new (OpCodes.Call, NoteEvents_SpinMiss)
            });

            return instructionsList;
        }

        [HarmonyPatch(typeof(TrackGameplayLogic), nameof(TrackGameplayLogic.UpdateScratchSectionState)), HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TrackGameplayLogic_UpdateScratchSectionState_Transpiler(IEnumerable<CodeInstruction> instructions) {
            var instructionsList = new List<CodeInstruction>(instructions);
            var NoteEvents_ScratchMiss = typeof(NoteEvents).GetMethod(nameof(ScratchMiss), BindingFlags.NonPublic | BindingFlags.Static);
            var ScoreState_DropMultiplier = typeof(PlayState.ScoreState).GetMethod(nameof(PlayState.ScoreState.DropMultiplier));
            var ScratchSection_noteIndex = typeof(ScratchSection).GetField(nameof(ScratchSection.noteIndex));

            var match = PatternMatching.Match(instructionsList, new Func<CodeInstruction, bool>[] {
                instr => instr.Calls(ScoreState_DropMultiplier)
            }).First()[0];
                
            instructionsList.InsertRange(match.End, new CodeInstruction[] {
                new (OpCodes.Ldloc_2), // section
                new (OpCodes.Ldfld, ScratchSection_noteIndex),
                new (OpCodes.Call, NoteEvents_ScratchMiss)
            });

            return instructionsList;
        }
    }
}