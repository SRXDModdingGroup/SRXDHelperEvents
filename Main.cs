using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace SRXDHelperEvents {
    [BepInPlugin("SRXD.HelperEvents", "HelperEvents", "1.0.0.0")]
    public class Main : BaseUnityPlugin {
        public static new ManualLogSource Logger { get; private set; }
        
        private void Awake() {
            Logger = base.Logger;
            
            var harmony = new Harmony("HelperEvents");
            
            harmony.PatchAll(typeof(NoteEvents));

            NoteEvents.OnNoteHit += args => Logger.LogMessage($"Hit note: {args.NoteIndex}, {args.Note.NoteType}, {args.TimeOffset:0.00}");
            NoteEvents.OnSustainedNoteTick += args => Logger.LogMessage($"Sustained note tick: {args.NoteIndex}, {args.Note.NoteType}");
            NoteEvents.OnOverbeat += () => Logger.LogMessage("Overbeat");
            NoteEvents.OnNoteMiss += args => {
                int endNoteIndex = args.EndNoteIndex;
                
                if (endNoteIndex >= 0)
                    Logger.LogMessage($"Missed note: {args.NoteIndex}, {args.Note.NoteType}, {endNoteIndex}, {args.EndNote.NoteType}");
                else
                    Logger.LogMessage($"Missed note: {args.NoteIndex}, {args.Note.NoteType}");
            };
            NoteEvents.OnSustainedNoteMiss += args => {
                int endNoteIndex = args.EndNoteIndex;
                
                if (endNoteIndex >= 0)
                    Logger.LogMessage($"Broke sustained note: {args.NoteIndex}, {args.Note.NoteType}, {endNoteIndex}, {args.EndNote.NoteType}");
                else
                    Logger.LogMessage($"Broke sustained note: {args.NoteIndex}, {args.Note.NoteType}");
            };
        }
    }
}