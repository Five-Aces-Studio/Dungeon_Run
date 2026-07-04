using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Health : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Slider slider;
    [SerializeField] private int totalHealth = 100;

    private int currentHealth;

    private void Start()
    {
        currentHealth = totalHealth;
        UpdateHealthUI();
    }

    private void UpdateHealthUI()
    {
        healthText.text = currentHealth + "/" + totalHealth;
        slider.maxValue = totalHealth;
        slider.value = currentHealth;
    }

    public void HealDamage(int amount)
    {
        if(amount <= 0)
        {
            return;
        }

        currentHealth += amount;

        if (currentHealth > totalHealth)
        {
            currentHealth = totalHealth;
        }
        UpdateHealthUI();
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        if (currentHealth < 0) currentHealth = 0;
        UpdateHealthUI();
    }

    public bool IsAlive()
    {
        return currentHealth > 0;
    }
}
