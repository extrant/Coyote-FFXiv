

using System;

namespace Coyote.Utils
{
    internal enum Emote : uint {
        ObjectSit = 96,
        Sleep = 88,
    }

    internal static class EmoteExt {
        internal static string Name(this Emote emote) => emote switch {
            Emote.ObjectSit => "坐下（椅子/台阶）",
            Emote.Sleep => "躺下入睡",
            _ => throw new ArgumentOutOfRangeException(nameof(emote), emote, null),
        };
    }
}

