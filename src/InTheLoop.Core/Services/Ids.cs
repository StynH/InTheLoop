using System.Security.Cryptography;

namespace InTheLoop.Core.Services;

internal static class Ids
{
    private const string Alphabet = "abcdefghjkmnpqrstuvwxyz23456789";

    /// <summary>Short and unambiguous, so an AI can repeat it and a human can read it aloud.</summary>
    public static string NewReviewId() => "r-" + RandomNumberGenerator.GetString(Alphabet, 6);

    public static string NewProjectId() => "p-" + RandomNumberGenerator.GetString(Alphabet, 6);

    public static string NewEntryId() => RandomNumberGenerator.GetString(Alphabet, 8);
}
