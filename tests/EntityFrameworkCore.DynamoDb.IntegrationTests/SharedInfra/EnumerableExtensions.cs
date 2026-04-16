namespace System.Linq.Enumerable;

public static class EnumerableExtensions
{
    extension<T>(IEnumerable<T> source)
    {
        public async Task ForEachAsync(Func<T, Task> action)
        {
            foreach (var item in source)
                await action(item);
        }
    }
}
