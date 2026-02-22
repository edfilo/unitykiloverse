using UnityEngine;
using UnityEngine.UI;

namespace KiloWorld.UI.Stories
{
    public class StoryProgressSegment : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Image backgroundImage;

        private void Awake()
        {
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }

            if (fillImage == null)
            {
                var fill = transform.Find("Fill");
                if (fill != null)
                {
                    fillImage = fill.GetComponent<Image>();
                }
            }

            if (fillImage != null)
            {
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
        }

        public void Initialize(Image newFillImage, Image newBackgroundImage)
        {
            fillImage = newFillImage;
            backgroundImage = newBackgroundImage;
        }

        public void SetProgress(float value)
        {
            if (fillImage != null)
            {
                fillImage.fillAmount = Mathf.Clamp01(value);
            }
        }

        public void SetColors(Color background, Color fill)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = background;
            }

            if (fillImage != null)
            {
                fillImage.color = fill;
            }
        }
    }
}
