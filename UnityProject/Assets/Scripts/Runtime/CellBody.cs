using System.Collections.Generic;
using UnityEngine;
using WormWorld.Genome;

namespace WormWorld.Runtime
{
    /// <summary>
    /// Component representing a single lattice cell generated from a genome.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CellBody : MonoBehaviour
    {
        private static readonly Dictionary<TissueType, Color> TissuePalette = new Dictionary<TissueType, Color>
        {
            { TissueType.Throat, new Color(0.95f, 0.55f, 0.35f) },
            { TissueType.Digestive, new Color(0.8f, 0.4f, 0.2f) },
            { TissueType.Brain, new Color(0.6f, 0.2f, 0.75f) },
            { TissueType.Eye, new Color(0.2f, 0.6f, 0.95f) },
            { TissueType.Reproductive, new Color(0.95f, 0.2f, 0.6f) },
            { TissueType.MuscleAnchor, new Color(0.9f, 0.3f, 0.25f) },
            { TissueType.Fat, new Color(1f, 0.9f, 0.3f) },
            { TissueType.Skin, new Color(0.8f, 0.75f, 0.6f) },
            { TissueType.Armor, new Color(0.5f, 0.55f, 0.6f) },
            { TissueType.PheromoneEmitter, new Color(0.1f, 0.9f, 0.4f) },
            { TissueType.PheromoneReceptor, new Color(0.15f, 0.55f, 0.3f) },
            { TissueType.NerveEnding, new Color(0.95f, 0.9f, 0.15f) }
        };

        [SerializeField]
        private int gridX;

        [SerializeField]
        private int gridY;

        [SerializeField]
        private TissueType tissueType;

        [SerializeField]
        private Rigidbody2D body;

        [SerializeField]
        private Collider2D collider2D;

        /// <summary>
        /// Grid column index (X coordinate) of the cell.
        /// </summary>
        public int GridX => gridX;

        /// <summary>
        /// Grid row index (Y coordinate) of the cell.
        /// </summary>
        public int GridY => gridY;

        /// <summary>
        /// Underlying tissue classification.
        /// </summary>
        public TissueType Tissue => tissueType;

        /// <summary>
        /// Physics body backing this cell.
        /// </summary>
        public Rigidbody2D Body => body;

        /// <summary>
        /// Collider associated with the cell.
        /// </summary>
        public Collider2D Collider => collider2D;

        /// <summary>
        /// World-space centre of the cell.
        /// </summary>
        public Vector2 WorldCenter => body != null ? body.worldCenterOfMass : (Vector2)transform.position;

        internal void Initialise(int x, int y, TissueType tissue, Rigidbody2D rigidbody2D, Collider2D collider)
        {
            gridX = x;
            gridY = y;
            tissueType = tissue;
            body = rigidbody2D;
            collider2D = collider;
        }

        private void OnDrawGizmos()
        {
            if (!TissuePalette.TryGetValue(tissueType, out var colour))
            {
                colour = Color.gray;
            }

            Vector3 size;
            if (collider2D is BoxCollider2D box)
            {
                size = new Vector3(box.size.x * transform.lossyScale.x, box.size.y * transform.lossyScale.y, 0.01f);
            }
            else
            {
                size = Vector3.one * 0.1f;
            }

            Gizmos.color = colour;
            Gizmos.DrawCube(transform.position, size);
            Gizmos.color = Color.black;
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
