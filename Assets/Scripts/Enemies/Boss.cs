using System.Collections;
using UnityEngine;

public class Boss : MonoBehaviour
{
    [SerializeField] private int attackDamage = 5;
    private Health bossHealth;

    private Animator animationController;

    private Vector3 originalPosition;

    [SerializeField] private GameObject bossSprite;

    private void Awake()
    {
        bossHealth = GetComponent<Health>();
        animationController = bossSprite.GetComponent<Animator>();
    }

    private void Start()
    {
        originalPosition = this.transform.position;
    }

    private void OnEnable()
    {
        BossEvents.OnBossHit += HandleBossHit;  
        TurnEvents.OnBossTurnStart += Attack;
    }

    private void OnDisable()
    {
        BossEvents.OnBossHit -= HandleBossHit;  
        TurnEvents.OnBossTurnStart -= Attack;
    }

    private void HandleBossHit(CardData cardData)
    {
        bossHealth.TakeDamage(cardData.attackPower);

        if(!bossHealth.IsAlive())
        {
            Die();
        }
    }

    private void Die()
    {
        animationController.Play("Die");
        BossEvents.BossDeath();
    }

    private void Attack()
    {
        StartCoroutine(BossAttackAnimation());
    }

    private IEnumerator BossAttackAnimation()
    {
        Debug.Log("Ataca");
        Vector3 targetPosition = new Vector3(0, 0, 0);

        float duration = 0.5f;
        float timeElapsed = 0f;

        Debug.Log(bossSprite.transform.position);
        
        while (timeElapsed < duration)
        {
            this.transform.position = Vector3.Lerp(originalPosition, targetPosition, timeElapsed / duration);
            Debug.Log("Moviendo");
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        animationController.Play("Attack");
        PlayerEvents.PlayerHit(attackDamage);

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
