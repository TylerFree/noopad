namespace Nopad.Services;

public interface ISearchReplaceService
{
    IEnumerable<(int start, int length)> FindAll(string text, string pattern, bool matchCase, bool wholeWord, bool regex);
    string ReplaceAll(string text, string pattern, string replacement, bool matchCase, bool wholeWord, bool regex);
}
