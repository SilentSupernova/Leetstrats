﻿using DataStructures;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Reboot
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private List<Player> m_Players;
        private int m_PlayerTurn;
        private int m_StartingPlayerTurn;
        private Map<Hex> m_Map;
        NavGraph<Hex> m_NavGraph;
        [SerializeField] private float m_DrawScale = 1.0f;
        [SerializeField] private Tile m_TilePrefab;

        [SerializeField, Range(0, 180)] int m_TurnLength = 30;
        [SerializeField, ReadOnly] int m_TurnCount = 0;
        private HexHeuristic m_Heuristic;
        public Player PlayerWithTurn { get { return m_Players[m_PlayerTurn]; } }
        public List<Player> PlayersWithoutTurn { get { return m_Players.Where(x => x != PlayerWithTurn).ToList(); } }

        private Layout m_Layout;

        private List<NavNode<Hex>> m_NodesThatAreInRangeToMove;
        private List<NavNode<Hex>> m_NodesThatAreInRangeToAttack;
        private List<NavNode<Hex>> m_NodesThatAreInRangeToAttackAfterMoving;

        [SerializeField] private string m_LevelName = "LEVEL.txt";
        string Path(string file) { return Application.dataPath + "/" + file; }

        public float TimeBeforeNextPlayersTurn { get { return PlayerWithTurn.RemainingTime; } }

        private void Awake()
        {
            m_Layout = new Layout(Layout.FLAT, new Vector2(1f, 1f), Vector2.zero);
            if (!Load(Path(m_LevelName), out m_Map))
            {
                throw new System.Exception("FAILED TO LOAD LEVEL");
            }
            else
            {
                Debug.Log(string.Format("Loaded Map With {0} Hexes", m_Map.Contents.Count));
                m_NavGraph = new DataStructures.NavGraph<Hex>();

                foreach (Hex hex in m_Map.Contents)
                {
                    NavNode<Hex> navNode = new NavNode<Hex>(hex, true, 1.0f);
                    m_NavGraph.Nodes.Add(navNode);
                }
                foreach (NavNode<Hex> navNode in m_NavGraph.Nodes)
                {
                    foreach (Hex neighbor in navNode.Data.AllNeighbors())
                    {
                        if (m_Map.Contains(neighbor))
                        {
                            navNode.Connected.Add(m_NavGraph.Nodes.Single(x => x.Data == neighbor));
                        }
                    }
                    Tile tile = Instantiate(m_TilePrefab, HexToWorld(navNode.Data), Quaternion.Euler(0, -180, -30));
                    tile.HexNode = navNode;
                    tile.transform.parent = this.transform;
                    tile.name = string.Format("{0},{1},{2}", navNode.Data.q, navNode.Data.r, navNode.Data.s);
                }
            }
            Begin();
        }

        private bool Load<T>(string filePath, out T loaded)
        {
            if (File.Exists(filePath))
            {
                StreamReader sr = new StreamReader(filePath);
                loaded = JsonUtility.FromJson<T>(sr.ReadToEnd());
                sr.Close();
                return true;
            }
            loaded = default(T);
            return false;
        }

        public void Begin()
        {
            foreach (Player player in m_Players)
            {
                player.Init(this);
                foreach (Unit unit in player.Units)
                {
                    Hex nearestHex = WorldToHex(unit.transform.position);
                    //m_NavGraph.Nodes.Where(x => !x.IsTraversible);
                    unit.OccupiedNode = m_NavGraph.Nodes.SingleOrDefault(x => x.Data == nearestHex);

                    if (unit.OccupiedNode == null)
                    {
                        throw new System.Exception("Unit on inaccessible node");
                    }
                    unit.OccupiedNode.IsTraversible = false;
                    unit.transform.position = HexToWorld(nearestHex);
                }
            }
            m_PlayerTurn = Random.Range(0, m_Players.Count);
            m_StartingPlayerTurn = m_PlayerTurn;
            m_TurnCount = 0;
            StartCoroutine(PlayerWithTurn.CountDown((float)m_TurnLength, OnTurnComplete));
        }

        public void OnTurnComplete()
        {
            m_PlayerTurn = (m_PlayerTurn + 1) % m_Players.Count;
            if (m_PlayerTurn == m_StartingPlayerTurn)
            {
                m_TurnCount++;
            }

            StartCoroutine(PlayerWithTurn.CountDown((float)m_TurnLength, OnTurnComplete));
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha8))
            {
                PlayerWithTurn.EndTurn();
            }
        }

        private void OnDrawGizmos()
        {
            if (m_Map != null)
            {
                foreach (NavNode<Hex> node in m_NavGraph.Nodes)
                {
                    DrawHex(node.Data, Color.grey, m_DrawScale);
                }
            }
            if (Application.isPlaying)
            {
                foreach (Unit unit in PlayerWithTurn.Units)
                {
                    DrawHex(unit.Position, Color.green, m_DrawScale);
                }
            }
        }

        public void DrawHex(Hex hex, Color color, float drawScale)
        {
            Gizmos.color = color;
            List<Vector2> points = m_Layout.PolygonCorners(hex, drawScale);
            for (int i = 0; i < 6; i++)
            {
                Gizmos.DrawLine(points[i], points[(i + 1) % 6]);
            }
        }

        public List<NavNode<Hex>> GetPath(Hex from, Hex to)
        {
            return m_NavGraph.GetPath(from, to, m_Heuristic);
        }

        public List<NavNode<Hex>> GetTilesInRange(Hex from, int range)
        {
            return m_NavGraph.Nodes.Where(x => x.Data.Distance(from) <= range).ToList();
            // m_NavGraph.GetNodesInRange(from, range);
        }

        public List<NavNode<Hex>> GetTilesInMovementRange(Hex from, int range)
        {
            return m_NavGraph.GetNodesInRange(from, range, m_Heuristic);
        }

        public Vector2 HexToWorld(Hex hex)
        {
            return m_Layout.HexToPixel(hex);
        }

        public Hex WorldToHex(Vector2 worldPosition)
        {
            return m_Layout.PixelToHex(worldPosition).HexRound();
        }
    }
}