using GooGalaxy.Runtime.Cards.Interfaces;
using UnityEngine;

namespace GooGalaxy.Runtime.Board.Models
{
    public class GridUnit : MonoBehaviour
    {
        public ICardData CardData { get; private set; }

        public void Initialize(ICardData cardData)
        {
            CardData = cardData;
        }
    }
}
