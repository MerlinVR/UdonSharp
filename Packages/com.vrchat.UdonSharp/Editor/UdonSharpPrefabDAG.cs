using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UdonSharpEditor
{
    /// <summary>
    /// Contains a set of Directed Acyclic Graphs that may or may not be connected at any point. The set of DAGs is rooted on prefabs that contain no nested prefabs and are not variants.
    /// If a prefab nests another prefab, the prefab is considered a 'child' of the prefab that it is nesting. Because the parents must be visited and resolved before the children.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    internal class UdonSharpPrefabDAG : IEnumerable<GameObject>
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

        public UdonSharpPrefabDAG(IEnumerable<GameObject> allPrefabRoots)
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
                    Debug.Assert(parent != vertex);
                    
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
                        
                        Debug.Assert(parentPrefab);
                        Debug.Assert(parentPrefab != child);

                        Vertex parent = _vertexLookup[parentPrefab];
                        
                        vertex.Parents.Add(parent);
                        parent.Children.Add(vertex);
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
        }

        /// <summary>
        /// Iterates the DAG in topological order where all parents are visited before their children.
        /// Will iterate orphan nodes that don't have any parents or children first.
        /// </summary>
        public IEnumerator<GameObject> GetEnumerator()
        {
            return _sortedVertices.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
