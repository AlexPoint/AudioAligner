using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioAligner.Classes.Util
{
    /*
     * retrieved code from https://www.assembla.com/code/sonido/subversion/nodes/4/sphinx4/src/sphinx4/edu/cmu/sphinx/util/Cache.java
     * because <T> generic type didn't seem to work across languages
     */
    /**
     * Provides a simple object cache.
     *
     * <p>Object stored in cache must properly implement {@link Object#hashCode hashCode} and {@link Object#equals equals}.
     *
     * <p><strong>Note that this class is not synchronized.</strong>
     * If multiple threads access a cache concurrently, and at least one of
     * the threads modifies the cache, it <i>must</i> be synchronized externally.
     * This is typically accomplished by synchronizing on some object that
     * naturally encapsulates the cache.
     */
    class Cache <T>
    {
        private Dictionary<T, T> map = new Dictionary<T, T>();

        private int hits = 0;

        /**
         * Puts the given object in the cache if it is not already present.
         *
         * <p>If the object is already cached, than the instance that exists in the cached is returned.
         * Otherwise, it is placed in the cache and null is returned.
         *
         * @param object object to cache
         * @return the cached object or null if the given object was not already cached
         */
        public T cache(T obj) {
            if(!map.ContainsKey(obj)) {
                map.Add(obj, obj);
            } else {
                hits++;
            }
            T result = map[obj];
            return result;
        }

        /**
         * Returns the number of cache hits, which is the number of times {@link #cache} was called
         * and returned an object that already existed in the cache.
         *
         * @return the number of cache hits
         */
        public int getHits() {
            return hits;
        }

        /**
         * Returns the number of cache misses, which is the number of times {@link #cache} was called
         * and returned null (after caching the object), effectively representing the size of the cache.
         *
         * @return the number of cache misses
         */
        public int getMisses() {
            return map.Count;
        }
    }
}
