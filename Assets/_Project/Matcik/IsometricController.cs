using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IsometricController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public LayerMask groundMask; // Назначьте в инспекторе слой "Ground"
    
    private Camera mainCamera;
    private Vector3 forwardDirection, rightDirection;

    void Start()
    {
        mainCamera = Camera.main;
        forwardDirection = mainCamera.transform.forward;
        forwardDirection.y = 0;
        forwardDirection.Normalize();
        rightDirection = Quaternion.Euler(0, 90, 0) * forwardDirection;
        //StartCoroutine(UpdateRotationWithDelay());
    }

    void Update()
    {
        HandleMovement();
    }

    void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        Vector3 moveDirection = (horizontal * rightDirection + vertical * forwardDirection).normalized;
        float deltaSpeed = moveSpeed * Time.deltaTime;
        transform.Translate(moveDirection * deltaSpeed, Space.World);
    }

    /*void HandleRotation()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, 100f, groundMask))
        {
            Vector3 targetPosition = hit.point;
            targetPosition.y = transform.position.y;
            
            Vector3 targetDir = (targetPosition - transform.position).normalized;
            if (targetDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDir);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, 
                    targetRotation, 
                    rotationSpeed * Time.deltaTime
                );
            }
        }
    }

    IEnumerator UpdateRotationWithDelay()
    {
        while (true)
        {
            HandleRotation();
            yield return new WaitForSeconds(0.05f);
        }
    }*/
}
