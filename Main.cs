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
            
            harmony.PatchAll(typeof(GameplayEvents));

            // GameplayEvents.OnNoteHit += args => {
            //     int endNoteIndex = args.EndNoteIndex;
            //     
            //     if (endNoteIndex >= 0)
            //         Logger.LogMessage($"Hit note: {args.NoteIndex}, {args.Note.NoteType}, {endNoteIndex}, {args.EndNote.NoteType}, {args.TimeOffset}");
            //     else
            //         Logger.LogMessage($"Hit note: {args.NoteIndex}, {args.Note.NoteType}, {args.TimeOffset}");
            // };
            // GameplayEvents.OnSustainedNoteTick += args => {
            //     int endNoteIndex = args.EndNoteIndex;
            //     
            //     if (endNoteIndex >= 0)
            //         Logger.LogMessage($"Sustained note tick: {args.NoteIndex}, {args.Note.NoteType}, {endNoteIndex}, {args.EndNote.NoteType}");
            //     else
            //         Logger.LogMessage($"Sustained note tick: {args.NoteIndex}, {args.Note.NoteType}");
            // };
            // GameplayEvents.OnOverbeat += () => Logger.LogMessage("Overbeat");
            // GameplayEvents.OnNoteMiss += args => {
            //     int endNoteIndex = args.EndNoteIndex;
            //     
            //     if (endNoteIndex >= 0)
            //         Logger.LogMessage($"Missed note: {args.NoteIndex}, {args.Note.NoteType}, {endNoteIndex}, {args.EndNote.NoteType}");
            //     else
            //         Logger.LogMessage($"Missed note: {args.NoteIndex}, {args.Note.NoteType}");
            // };
            // GameplayEvents.OnSustainedNoteFailed += args => {
            //     int endNoteIndex = args.EndNoteIndex;
            //     
            //     if (endNoteIndex >= 0)
            //         Logger.LogMessage($"Broke sustained note: {args.NoteIndex}, {args.Note.NoteType}, {endNoteIndex}, {args.EndNote.NoteType}");
            //     else
            //         Logger.LogMessage($"Broke sustained note: {args.NoteIndex}, {args.Note.NoteType}");
            // };
        }
    }
}