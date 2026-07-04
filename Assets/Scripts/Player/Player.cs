using System.Collections;
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private GameObject playerSprite;
    private Vector3 originalPosition;
    private Animator playerAnimator;

    private Health playerHealth;
    private ParticleSystem VFX;

    private void OnEnable() 
    {
        PlayerEvents.OnCardPlayed += HandleCardPlayed;
        PlayerEvents.OnPlayerHit += PlayerHit;
    }

    private void OnDisable() 
    {
        PlayerEvents.OnCardPlayed -= HandleCardPlayed;
        PlayerEvents.OnPlayerHit -= PlayerHit;
    }

    private void Awake()
    {
        playerAnimator = playerSprite.GetComponent<Animator>();
        playerHealth = GetComponent<Health>();
        VFX = playerSprite.GetComponentInChildren<ParticleSystem>();
    }

    private void Start()
    {
        originalPosition = this.transform.position;   
        Debug.Log(originalPosition);
    }

    private void PlayerHit(int damage)
    {
        playerHealth.TakeDamage(damage);
        if (!playerHealth.IsAlive())
        {
            Die();
        }
    }

    private void Die()
    {
        playerAnimator.Play("Die");
        PlayerEvents.PlayerDeath();
    }

    private void HandleCardPlayed(CardData cardData)
    {
        if (cardData.attackPower > 0)
        {
            Attack(cardData);
        }
        if (cardData.healPower > 0)
        {
            Heal(cardData);
        }
    }

    private void Heal(CardData cardData)
    {
        playerHealth.HealDamage(cardData.healPower);
        VFX.Play();
    }

    private void Attack(CardData cardData)
    {
        StartCoroutine(PlayerAttackAnimation(cardData));
    }

    private IEnumerator PlayerAttackAnimation(CardData cardData)
    {
        Debug.Log(originalPosition);
        Debug.Log(this.transform.position);
        Vector3 targetPosition = originalPosition + new Vector3(4f, 0, 0);

        float duration = 0.5f;
        float timeElapsed = 0f;

        while (timeElapsed < duration)
        {
            this.transform.position = Vector3.Lerp(originalPosition, targetPosition, timeElapsed / duration);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        playerAnimator.Play("Attack");
        BossEvents.BossHit(cardData);
        yield return new WaitForSeconds(0.5f);
        timeElapsed = 0f;

        while (timeElapsed < duration)
        {
            this.transform.position = Vector3.Lerp(targetPosition, originalPosition, timeElapsed / duration);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        yield return null;
    }
}
