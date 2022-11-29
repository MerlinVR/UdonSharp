using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UnityEditor;
using UnityEngine;

namespace UdonSharpEditor
{
    /// <summary>
    /// Contains a set of Directed Acyclic Graphs that may or may not be connected at any point. The set of DAGs is rooted on prefabs that contain no nested prefabs and are not variants.
    /// If a prefab nests another prefab, the prefab is considered a 'child' of the prefab that it is nesting. Because the parents must be visited and resolved before the children.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    internal class UdonSharpPrefabDAG : IEnumerable<string>
    {
        private class Vertex
        {
            public GameObject Prefab;
            public List<Vertex> Children = new List<Vertex>();
            public List<Vertex> Parents = new List<Vertex>();
        }

        private List<Vertex> _vertices = new List<Vertex>();
        private Dictionary<GameObject, Vertex> _vertexLookup = new Dictionary<GameObject, Vertex>();
        private List<GameObject> _sortedVertices = new List<GameObject>();
        private List<string> _sortedPaths = new List<string>();

        public UdonSharpPrefabDAG(IEnumerable<GameObject> allPrefabRoots)
        {
            try
            {
                foreach (GameObject prefabRoot in allPrefabRoots)
                {
                    Vertex vert = new Vertex() { Prefab = prefabRoot };
                    _vertices.Add(vert);
                    _vertexLookup.Add(prefabRoot, vert);
                }
                
                foreach (Vertex vertex in _vertices)
                {
                    if (PrefabUtility.IsPartOfVariantPrefab(vertex.Prefab))
                    {
                        Vertex parent = _vertexLookup[PrefabUtility.GetCorrespondingObjectFromSource(vertex.Prefab)];

                        if (parent == vertex)
                        {
                            throw new Exception($"Parent of vertex cannot be the same as the vertex '{vertex.Prefab}'");
                        }
                        
                        vertex.Parents.Add(parent);
                        parent.Children.Add(vertex);
                    }

                    foreach (GameObject child in vertex.Prefab.GetComponentsInChildren<Transform>(true).Select(e => e.gameObject))
                    {
                        if (child == vertex.Prefab)
                        {
                            continue;
                        }

                        if (PrefabUtility.IsAnyPrefabInstanceRoot(child))
                        {
                            GameObject parentPrefab = PrefabUtility.GetCorrespondingObjectFromSource(child);

                            if (parentPrefab == null)
                            {
                                throw new Exception($"ParentPrefab of '{child}' is null");
                            }

                            parentPrefab = parentPrefab.transform.root.gameObject;

                            if (parentPrefab == child)
                            {
                                throw new Exception($"ParentPrefab cannot be the same as child '{child}'");
                            }

                            // If a nested prefab is referenced that does *not* have any UdonBehaviours on it, it will not be in the vertex list, and does not need to be linked.
                            if (_vertexLookup.TryGetValue(parentPrefab, out Vertex parent))
                            {
                                vertex.Parents.Add(parent);
                                parent.Children.Add(vertex);
                            }
                        }
                    }
                }
                
                // Do sorting
                HashSet<Vertex> visitedVertices = new HashSet<Vertex>();

                // Orphaned nodes with no parents or children go first
                foreach (Vertex vertex in _vertices)
                {
                    if (vertex.Children.Count == 0 && vertex.Parents.Count == 0)
                    {
                        visitedVertices.Add(vertex);
                        _sortedVertices.Add(vertex.Prefab);
                    }
                }

                Queue<Vertex> openSet = new Queue<Vertex>();

                // Find root nodes with no parents
                foreach (Vertex vertex in _vertices)
                {
                    if (!visitedVertices.Contains(vertex) && vertex.Parents.Count == 0)
                    {
                        openSet.Enqueue(vertex);
                    }
                }

                while (openSet.Count > 0)
                {
                    Vertex vertex = openSet.Dequeue();

                    if (visitedVertices.Contains(vertex))
                    {
                        continue;
                    }

                    if (vertex.Parents.Count > 0)
                    {
                        bool neededParentVisit = false;

                        foreach (Vertex vertexParent in vertex.Parents)
                        {
                            if (!visitedVertices.Contains(vertexParent))
                            {
                                neededParentVisit = true;
                                openSet.Enqueue(vertexParent);
                            }
                        }

                        if (neededParentVisit)
                        {
                            // Re-queue to visit after we have traversed the node's parents
                            openSet.Enqueue(vertex);
                            continue;
                        }
                    }

                    visitedVertices.Add(vertex);
                    _sortedVertices.Add(vertex.Prefab);

                    foreach (Vertex vertexChild in vertex.Children)
                    {
                        openSet.Enqueue(vertexChild);
                    }
                }

                // Sanity check
                foreach (Vertex vertex in _vertices)
                {
                    if (!visitedVertices.Contains(vertex))
                    {
                        throw new Exception($"Invalid DAG state: node '{vertex.Prefab}' was not visited.");
                    }
                }

                _sortedPaths = _sortedVertices.Select(AssetDatabase.GetAssetPath).ToList();
            }
            catch (Exception e)
            {
                UdonSharpUtils.LogError($"Exception while sorting prefabs for upgrade. Falling back to non-sorted set, nested prefabs may not upgrade properly. Exception: {e}");
                _sortedPaths = allPrefabRoots.Select(AssetDatabase.GetAssetPath).ToList();
            }
        }

        /// <summary>
        /// Iterates the DAG in topological order where all parents are visited before their children.
        /// Will iterate orphan nodes that don't have any parents or children first.
        /// We return paths here because of the assumption that Unity may create a new set of prefab objects when reimporting a prefab after saving it.
        /// The upgrade process is expected to load the prefabs from their path in sequence of upgrade
        /// </summary>
        public IEnumerator<string> GetEnumerator()
        {
            return _sortedPaths.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
