using System.Collections.Generic;
using player2_sdk;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class ExampleCustomCharacterScript : MonoBehaviour
{
    [SerializeField] private CustomCharacters customCharacters;
    [SerializeField] private TMP_Dropdown dropdown;

    public UnityEvent<Character, string> OnChangedCustomCharacter = new();

    private readonly List<(Character, string)> Npcs = new();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        dropdown.MultiSelect = false;

        customCharacters.OnNpcSpawned.AddListener((character, npcId) =>
        {
            Debug.Log($"Spawned {character.short_name}");
            Npcs.Add((character, npcId));
            dropdown.AddOptions(new List<string> { character.short_name });
        });
        dropdown.onValueChanged.AddListener(index =>
        {
            var character = customCharacters.GetCharacterByName(dropdown.options[index].text);
            var id = customCharacters.GetNpcIdForCharacter(character);

            OnChangedCustomCharacter.Invoke(character, id);
        });
    }


    // Update is called once per frame
    private void Update()
    {
    }
}