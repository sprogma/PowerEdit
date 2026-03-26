using EditorFramework.Widgets;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorFramework.Layout
{
    public interface ILayoutManager
    {
        public void Resize(BaseWindow window, Rect NewSize);
        public void UpdateScale(BaseWindow window, double scale);
        public Rect Position { get; }
        public long? PageStepSize { get; }
    }

    public static class LayoutRegistry
    {
        private static readonly Dictionary<Type, Func<ILayoutManager>> Providers = [];
        private static readonly Dictionary<Type, Func<ILayoutManager>> ResolvedCache = [];

        public static void Register<T>(Func<ILayoutManager> provider) where T : BaseWindow
            => Providers[typeof(T)] = provider;

        public static Func<ILayoutManager> Resolve(Type widgetType)
        {
            if (ResolvedCache.TryGetValue(widgetType, out var cached)) return cached;

            Type? current = widgetType;
            while (current != null && typeof(BaseWindow).IsAssignableFrom(current))
            {
                if (Providers.TryGetValue(current, out var provider))
                {
                    ResolvedCache[widgetType] = provider;
                    return provider;
                }
                current = current.BaseType;
            }

            throw new Exception($"No LayoutManager was registred for {widgetType.Name} or it's base classes.");
        }
    }
    public static class GetLayout<T> where T : BaseWindow
    {
        private static Func<ILayoutManager>? CachedProvider;

        public static ILayoutManager Value { get {
            CachedProvider ??= LayoutRegistry.Resolve(typeof(T));
            return CachedProvider();
        } }
    }
}
