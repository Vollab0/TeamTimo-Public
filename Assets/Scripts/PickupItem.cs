using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PickupItem : MonoBehaviour
{
    public Inventory inventory;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Item"))
        {
            Vector3 directionToItem = other.transform.position - transform.position;
            float angle = Vector3.Angle(transform.forward, directionToItem);

            if (angle < 45f) // Réglez cet angle selon vos besoins
            {
                Debug.Log("Item is in front of us");

                if (Input.GetKeyDown(KeyCode.F))
                {
                    inventory.content.Add(other.gameObject.GetComponent<Item>().item);
                    Destroy(other.gameObject);
                }
            }
        }
    }
}