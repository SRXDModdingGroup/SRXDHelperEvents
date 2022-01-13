using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SMU.Utilities;

namespace SRXDHelperEvents {
    public static class GameplayEvents {
        public class NoteEventArgs {
            public int NoteIndex { get; }
            
            public int EndNoteIndex { get; }
            
            public Note Note { get; }
            
            public Note EndNote { get; }
            
            public float TimeOffset { get; }

            internal NoteEventArgs(int noteIndex, int endNoteIndex, Note note, Note endNote, float timeOffset) {
                NoteIndex = noteIndex;
                EndNoteIndex = endNoteIndex;
                Note = note;
                EndNote = endNote;
                TimeOffset = timeOffset;
            }
        }
        
        public static bool Playing { get; private set; }
        
        public static event Action<NoteEventArgs> OnNoteHit;

        public static event Action<NoteEventArgs> OnSustainedNoteTick;

        public static event Action OnOverbeat;

        public static event Action<NoteEventArgs> OnNoteMiss;

        public static event Action<NoteEventArgs> OnSustainedNoteFailed;

        private static PlayableTrackData trackData;
        
        private static void NormalNoteHit(int noteIndex, Note note, float timeOffset) {
            if (!Playing)
                return;

            if (note.NoteType == NoteType.Match)
                OnNoteHit?.Invoke(new NoteEventArgs(noteIndex, -1, note, new Note(), 0f));
            else if (note.length > 0f) {
                int endNoteIndex = note.endNoteIndex;

                OnNoteHit?.Invoke(new NoteEventArgs(noteIndex, endNoteIndex, note, trackData.GetNote(endNoteIndex), timeOffset));
            }
            else
                OnNoteHit?.Invoke(new NoteEventArgs(noteIndex, -1, note, new Note(), timeOffset));
        }

        private static void BeatReleaseHit(int noteIndex, Note note, float timeOffset) {
            if (Playing)
                OnNoteHit?.Invoke(new NoteEventArgs(noteIndex, -1, note, new Note(), timeOffset));
        }

        private static void HoldHit(int noteIndex, int endNoteIndex, float timeOffset) {
            if (Playing)
                OnNoteHit?.Invoke(new NoteEventArgs(noteIndex, endNoteIndex, trackData.GetNote(noteIndex), trackData.GetNote(endNoteIndex), timeOffset));
        }

        private static void LiftoffHit(int noteIndex, float timeOffset) {
            if (Playing)
                OnNoteHit?.Invoke(new NoteEventArgs(noteIndex, -1, trackData.GetNote(noteIndex), new Note(), timeOffset));
        }

        private static void SpinHit(int noteIndex, Note note) {
            if (Playing)
                OnNoteHit?.Invoke(new NoteEventArgs(noteIndex, -1, note, new Note(), 0f));
        }

        private static void HoldTick(int noteIndex, int endNoteIndex) {
            if (Playing)
                OnSustainedNoteTick?.Invoke(new NoteEventArgs(noteIndex, endNoteIndex, trackData.GetNote(noteIndex), trackData.GetNote(endNoteIndex), 0f));
        }
        
        private static void BeatHoldTick(int noteIndex, int endNoteIndex, Note note, Note endNote) {
            if (Playing)
                OnSustainedNoteTick?.Invoke(new NoteEventArgs(noteIndex, endNoteIndex, note, endNote, 0f));
        }
        
        private static void SpinTick(int noteIndex, Note note) {
            if (Playing)
                OnSustainedNoteTick?.Invoke(new NoteEventArgs(noteIndex, -1, note, new Note(), 0f));
        }

        private static void ScratchTick(int noteIndex) {
            if (Playing)
                OnSustainedNoteTick?.Invoke(new NoteEventArgs(noteIndex, -1, trackData.GetNote(noteIndex), new Note(), 0f));
        }

        private static void Overbeat() {
            if (Playing)
                OnOverbeat?.Invoke();
        }

        private static void NoteMiss(int noteIndex, Note note) {
            if (Playing)
                OnNoteMiss?.Invoke(new NoteEventArgs(noteIndex, -1, note, new Note(), 0f));
        }

        private static void BeatMiss(int noteIndex, Note note) {
            if (!Playing)
                return;
            
            if (note.length > 0f) {
                int endNoteIndex = note.endNoteIndex;
                
                OnNoteMiss?.Invoke(new NoteEventArgs(noteIndex, endNoteIndex, note, trackData.GetNote(endNoteIndex), 0f));
            }
            else
                OnNoteMiss?.Invoke(new NoteEventArgs(noteIndex, -1, note, new Note(), 0f));
        }
        
        private static void BeatHoldMiss(int noteIndex, int endNoteIndex, Note note, Note endNote) {
            if (Playing)
                OnSustainedNoteFailed?.Invoke(new NoteEventArgs(noteIndex, endNoteIndex, note, endNote, 0f));
        }

        private static void HoldMiss(int noteIndex, int endNoteIndex, bool hasEntered) {
            if (!Playing)
                return;
            
            if (hasEntered)
                OnSustainedNoteFailed?.Invoke(new NoteEventArgs(noteIndex, endNoteIndex, trackData.GetNote(noteIndex), trackData.GetNote(endNoteIndex), 0f));
            else
                OnNoteMiss?.Invoke(new NoteEventArgs(noteIndex, endNoteIndex, trackData.GetNote(noteIndex), trackData.GetNote(endNoteIndex), 0f));
        }

        private static void LiftoffMiss(int noteIndex) {
            if (Playing)
                OnNoteMiss?.Invoke(new NoteEventArgs(noteIndex, -1, trackData.GetNote(noteIndex), new Note(), 0f));
        }

        private static void SpinMiss(int noteIndex, Note note, bool failedInitialSpin) {
            if (!Playing)
                return;
            
            if (failedInitialSpin)
                OnNoteMiss?.Invoke(new NoteEventArgs(noteIndex, -1, note, new Note(), 0f));
            else
                OnSustainedNoteFailed?.Invoke(new NoteEventArgs(noteIndex, -1, note, new Note(), 0f));
        }

        private static void ScratchMiss(int noteIndex) {
            if (Playing)
                OnSustainedNoteFailed?.Invoke(new NoteEventArgs(noteIndex, -1, trackData.GetNote(noteIndex), new Note(), 0f));
        }

        [HarmonyPatch(typeof(Track), nameof(Track.PlayTrack)), HarmonyPostfix]
        private static void Track_PlayTrack_Postfix(Track __instance) {
            trackData = __instance.playStateFirst.trackData;
            Playing = true;
        }
        
        [HarmonyPatch(typeof(Track), nameof(Track.PracticeTrack)), HarmonyPostfix]
        private static void Track_PracticeTrack_Postfix(Track __instance) {
            trackData = __instance.playStateFirst.trackData;
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

        [HarmonyPatch(typeof(Track), nameof(Track.Update)), HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Track_Update_Transpiler(IEnumerable<CodeInstruction> instructions) {
            var instructionsList = new List<CodeInstruction>(instructions);
            var GameplayEvents_Overbeat = typeof(GameplayEvents).GetMethod(nameof(Overbeat), BindingFlags.NonPublic | BindingFlags.Static);
            var ScoreState_DropMultiplier = typeof(PlayState.ScoreState).GetMethod(nameof(PlayState.ScoreState.DropMultiplier));

            var match = PatternMatching.Match(instructionsList, new Func<CodeInstruction, bool>[] {
                instr => instr.Calls(ScoreState_DropMultiplier)
            }).First()[0];
            
            instructionsList.Insert(match.End, new CodeInstruction(OpCodes.Call, GameplayEvents_Overbeat));

            return instructionsList;
        }
        
        [HarmonyPatch(typeof(TrackGameplayLogic), nameof(TrackGameplayLogic.UpdateNoteState)), HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TrackGameplayLogic_UpdateNoteState_Transpiler(IEnumerable<CodeInstruction> instructions) {
            var instructionsList = new List<CodeInstruction>(instructions);
            var insertions = new DeferredInsertion<CodeInstruction>();
            var GameplayEvents_NormalNoteHit = typeof(GameplayEvents).GetMethod(nameof(NormalNoteHit), BindingFlags.NonPublic | BindingFlags.Static);
            var GameplayEvents_BeatReleaseHit = typeof(GameplayEvents).GetMethod(nameof(BeatReleaseHit), BindingFlags.NonPublic | BindingFlags.Static);
            var GameplayEvents_BeatHoldTick = typeof(GameplayEvents).GetMethod(nameof(BeatHoldTick), BindingFlags.NonPublic | BindingFlags.Static);
            var GameplayEvents_NoteMiss = typeof(GameplayEvents).GetMethod(nameof(NoteMiss), BindingFlags.NonPublic | BindingFlags.Static);
            var GameplayEvents_BeatMiss = typeof(GameplayEvents).GetMethod(nameof(BeatMiss), BindingFlags.NonPublic | BindingFlags.Static);
            var GameplayEvents_BeatHoldMiss = typeof(GameplayEvents).GetMethod(nameof(BeatHoldMiss), BindingFlags.NonPublic | BindingFlags.Static);
            var ScoreState_AddScoreIfPossible = typeof(TrackGameplayLogic).GetMethod("AddScoreIfPossible", BindingFlags.NonPublic | BindingFlags.Static);
            var ScoreState_DropMultiplier = typeof(PlayState.ScoreState).GetMethod(nameof(PlayState.ScoreState.DropMultiplier));
            var TrackGameplayLogic_AllowErrorToOccur = typeof(TrackGameplayLogic).GetMethod(nameof(TrackGameplayLogic.AllowErrorToOccur));
            var Note_endNoteIndex = typeof(Note).GetField(nameof(Note.endNoteIndex));

            var matches = PatternMatching.Match(instructionsList, new Func<CodeInstruction, bool>[] {
                instr => instr.opcode == OpCodes.Ldarg_0
            }).Then(new Func<CodeInstruction, bool>[] {
                instr => instr.Calls(ScoreState_AddScoreIfPossible)
            });

            foreach (var match in matches) {
                var opcode = instructionsList[match[0].Start + 1].opcode;

                if (opcode == OpCodes.Ldloc_S) { // pointsToAdd
                    insertions.Add(match[1].End, new CodeInstruction[] {
                        new (OpCodes.Ldarg_2), // noteIndex
                        new (OpCodes.Ldloc_1), // note
                        new (OpCodes.Ldloc_S, (byte) 7), // timeOffset
                        new (OpCodes.Call, GameplayEvents_NormalNoteHit)
                    });
                }
                else if (opcode == OpCodes.Ldloc_3) { // gameplayVariables
                    insertions.Add(match[1].End, new CodeInstruction[] {
                        new (OpCodes.Ldloc_1), // note
                        new (OpCodes.Ldfld, Note_endNoteIndex),
                        new (OpCodes.Ldloc_S, (byte) 42), // endNote
                        new (OpCodes.Ldloc_S, (byte) 46), // beatTimeOffset
                        new (OpCodes.Call, GameplayEvents_BeatReleaseHit)
                    });
                }
                else { // 1
                    insertions.Add(match[1].End, new CodeInstruction[] {
                        new (OpCodes.Ldarg_2), // noteIndex
                        new (OpCodes.Ldloc_1), // note
                        new (OpCodes.Ldfld, Note_endNoteIndex),
                        new (OpCodes.Ldloc_1), // note
                        new (OpCodes.Ldloc_S, (byte) 42), // endNote
                        new (OpCodes.Call, GameplayEvents_BeatHoldTick)
                    });
                }
            }

            matches = PatternMatching.Match(instructionsList, new Func<CodeInstruction, bool>[] {
                instr => instr.Calls(ScoreState_DropMultiplier)
            });

            foreach (var match in matches) {
                var result = match[0];

                if (instructionsList[result.Start - 4].Branches(out _)) {
                    insertions.Add(result.End, new CodeInstruction[] {
                        new (OpCodes.Ldloc_1), // note
                        new (OpCodes.Ldfld, Note_endNoteIndex),
                        new (OpCodes.Ldloc_S, (byte) 42), // endNote
                        new (OpCodes.Call, GameplayEvents_NoteMiss)
                    });
                }
                else {
                    insertions.Add(result.End, new CodeInstruction[] {
                        new (OpCodes.Ldarg_2), // noteIndex
                        new (OpCodes.Ldloc_1), // note
                        new (OpCodes.Ldfld, Note_endNoteIndex),
                        new (OpCodes.Ldloc_1), // note
                        new (OpCodes.Ldloc_S, (byte) 42), // endNote
                        new (OpCodes.Call, GameplayEvents_BeatHoldMiss)
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
                    new (OpCodes.Call, GameplayEvents_BeatMiss)
                });
            }
            
            insertions.Insert(instructionsList);

            return instructionsList;
        }

        [HarmonyPatch(typeof(TrackGameplayLogic), nameof(TrackGameplayLogic.UpdateFreestyleSectionState)), HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TrackGameplayLogic_UpdateFreestyleSectionState_Transpiler(IEnumerable<CodeInstruction> instructions) {
            var instructionsList = new List<CodeInstruction>(instructions);
            var insertions = new DeferredInsertion<CodeInstruction>();
            var GameplayEvents_HoldHit = typeof(GameplayEvents).GetMethod(nameof(HoldHit), BindingFlags.NonPublic | BindingFlags.Static);
            var GameplayEvents_LiftoffHit = typeof(GameplayEvents).GetMethod(nameof(LiftoffHit), BindingFlags.NonPublic | BindingFlags.Static);
            var GameplayEvents_HoldTick = typeof(GameplayEvents).GetMethod(nameof(HoldTick), BindingFlags.NonPublic | BindingFlags.Static);
            var GameplayEvents_HoldMiss = typeof(GameplayEvents).GetMethod(nameof(HoldMiss), BindingFlags.NonPublic | BindingFlags.Static);
            var GameplayEvents_LiftoffMiss = typeof(GameplayEvents).GetMethod(nameof(LiftoffMiss), BindingFlags.NonPublic | BindingFlags.Static);
            var ScoreState_AddScoreIfPossible = typeof(TrackGameplayLogic).GetMethod("AddScoreIfPossible", BindingFlags.NonPublic | BindingFlags.Static);
            var TrackGameplayLogic_AllowErrorToOccur = typeof(TrackGameplayLogic).GetMethod(nameof(TrackGameplayLogic.AllowErrorToOccur));
            var FreestyleSection_firstNoteIndex = typeof(FreestyleSection).GetField(nameof(FreestyleSection.firstNoteIndex));
            var FreestyleSection_endNoteIndex = typeof(FreestyleSection).GetField(nameof(FreestyleSection.endNoteIndex));
            var FreestyleSectionState_hasEntered = typeof(FreestyleSectionState).GetField(nameof(FreestyleSectionState.hasEntered));
            var FreestyleSectionState_releaseState = typeof(FreestyleSectionState).GetField(nameof(FreestyleSectionState.releaseState));
            var Worms_tapScore = typeof(GameplayVariables.Worms).GetField(nameof(GameplayVariables.Worms.tapScore));
            
            var matches = PatternMatching.Match(instructionsList, new Func<CodeInstruction, bool>[] {
                instr => instr.opcode == OpCodes.Ldarg_0
            }).Then(new Func<CodeInstruction, bool>[] {
                instr => instr.Calls(ScoreState_AddScoreIfPossible)
            });

            foreach (var match0 in matches) {
                int start = match0[0].Start;
                
                if (instructionsList[start + 1].opcode == OpCodes.Ldloc_S) { // worms
                    if (instructionsList[start + 2].LoadsField(Worms_tapScore)) {
                        insertions.Add(match0[1].End, new CodeInstruction[] {
                            new (OpCodes.Ldloc_S, (byte) 6), // section
                            new (OpCodes.Ldfld, FreestyleSection_firstNoteIndex),
                            new (OpCodes.Ldloc_S, (byte) 6), // section
                            new (OpCodes.Ldfld, FreestyleSection_endNoteIndex),
                            new (OpCodes.Ldloc_S, (byte) 50), // timeOffset
                            new (OpCodes.Call, GameplayEvents_HoldHit)
                        });
                    }
                    else {
                        insertions.Add(match0[1].End, new CodeInstruction[] {
                            new (OpCodes.Ldloc_S, (byte) 6), // section
                            new (OpCodes.Ldfld, FreestyleSection_endNoteIndex),
                            new (OpCodes.Ldloc_S, (byte) 53), // timeOffset2
                            new (OpCodes.Call, GameplayEvents_LiftoffHit)
                        });
                    }
                }
                else {
                    insertions.Add(match0[1].End, new CodeInstruction[] {
                        new (OpCodes.Ldloc_S, (byte) 6), // section
                        new (OpCodes.Ldfld, FreestyleSection_firstNoteIndex),
                        new (OpCodes.Ldloc_S, (byte) 6), // section
                        new (OpCodes.Ldfld, FreestyleSection_endNoteIndex),
                        new (OpCodes.Call, GameplayEvents_HoldTick)
                    });
                }
            }
            
            var match1 = PatternMatching.Match(instructionsList, new Func<CodeInstruction, bool>[] {
                instr => instr.Calls(TrackGameplayLogic_AllowErrorToOccur),
                instr => instr.opcode == OpCodes.Pop
            }).Then(new Func<CodeInstruction, bool>[] {
                instr => instr.labels.Count > 0
            }).First()[1];
            
            insertions.Add(match1.End, new CodeInstruction[] {
                new (OpCodes.Ldloc_S, (byte) 6), // section
                new (OpCodes.Ldfld, FreestyleSection_firstNoteIndex),
                new (OpCodes.Ldloc_S, (byte) 6), // section
                new (OpCodes.Ldfld, FreestyleSection_endNoteIndex),
                new (OpCodes.Ldarg_S, (byte) 4), // state
                new (OpCodes.Ldfld, FreestyleSectionState_hasEntered),
                new (OpCodes.Call, GameplayEvents_HoldMiss)
            });

            match1 = PatternMatching.Match(instructionsList, new Func<CodeInstruction, bool>[] {
                instr => instr.IsLdarg(), // state
                instr => instr.opcode == OpCodes.Ldc_I4_5, // ReleaseState.Failed
                instr => instr.StoresField(FreestyleSectionState_releaseState)
            }).First()[0];
            
            insertions.Add(match1.End, new CodeInstruction[] {
                new (OpCodes.Ldloc_S, (byte) 6), // section
                new (OpCodes.Ldfld, FreestyleSection_endNoteIndex),
                new (OpCodes.Call, GameplayEvents_LiftoffMiss)
            });
            
            insertions.Insert(instructionsList);

            return instructionsList;
        }

        [HarmonyPatch(typeof(TrackGameplayLogic), nameof(TrackGameplayLogic.UpdateSpinSectionState)), HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TrackGameplayLogic_UpdateSpinSectionState_Transpiler(IEnumerable<CodeInstruction> instructions) {
            var instructionsList = new List<CodeInstruction>(instructions);
            var insertions = new DeferredInsertion<CodeInstruction>();
            var GameplayEvents_SpinHit = typeof(GameplayEvents).GetMethod(nameof(SpinHit), BindingFlags.NonPublic | BindingFlags.Static);
            var GameplayEvents_SpinTick = typeof(GameplayEvents).GetMethod(nameof(SpinTick), BindingFlags.NonPublic | BindingFlags.Static);
            var GameplayEvents_SpinMiss = typeof(GameplayEvents).GetMethod(nameof(SpinMiss), BindingFlags.NonPublic | BindingFlags.Static);
            var ScoreState_AddScoreIfPossible = typeof(TrackGameplayLogic).GetMethod("AddScoreIfPossible", BindingFlags.NonPublic | BindingFlags.Static);
            var TrackGameplayLogic_AllowErrorToOccur = typeof(TrackGameplayLogic).GetMethod(nameof(TrackGameplayLogic.AllowErrorToOccur));
            var SpinnerSection_noteIndex = typeof(ScratchSection).GetField(nameof(SpinnerSection.noteIndex));
            var SpinSectionState_failedInitialSpin = typeof(SpinSectionState).GetField(nameof(SpinSectionState.failedInitialSpin));
            
            var matches = PatternMatching.Match(instructionsList, new Func<CodeInstruction, bool>[] {
                instr => instr.opcode == OpCodes.Ldarg_0
            }).Then(new Func<CodeInstruction, bool>[] {
                instr => instr.Calls(ScoreState_AddScoreIfPossible)
            });

            foreach (var match0 in matches) {
                if (instructionsList[match0[0].Start + 1].opcode == OpCodes.Ldloc_2) { // spins
                    insertions.Add(match0[1].End, new CodeInstruction[] {
                        new (OpCodes.Ldloc_3), // section
                        new (OpCodes.Ldfld, SpinnerSection_noteIndex),
                        new (OpCodes.Ldloc_S, (byte) 4), // note
                        new (OpCodes.Call, GameplayEvents_SpinHit)
                    });
                }
                else {
                    insertions.Add(match0[1].End, new CodeInstruction[] {
                        new (OpCodes.Ldloc_3), // section
                        new (OpCodes.Ldfld, SpinnerSection_noteIndex),
                        new (OpCodes.Ldloc_S, (byte) 4), // note
                        new (OpCodes.Call, GameplayEvents_SpinTick)
                    });
                }
            }
            
            var match1 = PatternMatching.Match(instructionsList, new Func<CodeInstruction, bool>[] {
                instr => instr.Calls(TrackGameplayLogic_AllowErrorToOccur),
                instr => instr.opcode == OpCodes.Pop
            }).First()[0];

            insertions.Add(match1.End, new CodeInstruction[] {
                new (OpCodes.Ldloc_3), // section
                new (OpCodes.Ldfld, SpinnerSection_noteIndex),
                new (OpCodes.Ldloc_S, (byte) 4), // note
                new (OpCodes.Ldarg_S, (byte) 4), // state
                new (OpCodes.Ldfld, SpinSectionState_failedInitialSpin),
                new (OpCodes.Call, GameplayEvents_SpinMiss)
            });
            
            insertions.Insert(instructionsList);

            return instructionsList;
        }

        [HarmonyPatch(typeof(TrackGameplayLogic), nameof(TrackGameplayLogic.UpdateScratchSectionState)), HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TrackGameplayLogic_UpdateScratchSectionState_Transpiler(IEnumerable<CodeInstruction> instructions) {
            var instructionsList = new List<CodeInstruction>(instructions);
            var insertions = new DeferredInsertion<CodeInstruction>();
            var GameplayEvents_ScratchTick = typeof(GameplayEvents).GetMethod(nameof(ScratchTick), BindingFlags.NonPublic | BindingFlags.Static);
            var GameplayEvents_ScratchMiss = typeof(GameplayEvents).GetMethod(nameof(ScratchMiss), BindingFlags.NonPublic | BindingFlags.Static);
            var ScoreState_AddScoreIfPossible = typeof(TrackGameplayLogic).GetMethod("AddScoreIfPossible", BindingFlags.NonPublic | BindingFlags.Static);
            var ScoreState_DropMultiplier = typeof(PlayState.ScoreState).GetMethod(nameof(PlayState.ScoreState.DropMultiplier));
            var ScratchSection_noteIndex = typeof(ScratchSection).GetField(nameof(ScratchSection.noteIndex));
            
            var match = PatternMatching.Match(instructionsList, new Func<CodeInstruction, bool>[] {
                instr => instr.Calls(ScoreState_AddScoreIfPossible)
            }).First()[0];
            
            insertions.Add(match.End, new CodeInstruction[] {
                new (OpCodes.Ldloc_2), // section
                new (OpCodes.Ldfld, ScratchSection_noteIndex),
                new (OpCodes.Call, GameplayEvents_ScratchTick)
            });

            match = PatternMatching.Match(instructionsList, new Func<CodeInstruction, bool>[] {
                instr => instr.Calls(ScoreState_DropMultiplier)
            }).First()[0];
                
            insertions.Add(match.End, new CodeInstruction[] {
                new (OpCodes.Ldloc_2), // section
                new (OpCodes.Ldfld, ScratchSection_noteIndex),
                new (OpCodes.Call, GameplayEvents_ScratchMiss)
            });
            
            insertions.Insert(instructionsList);

            return instructionsList;
        }
    }
}