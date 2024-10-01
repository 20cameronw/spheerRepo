using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{

    public Sound[] sounds;

    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    void Awake()
    {
        foreach (Sound s in sounds)
        {
            s.source = gameObject.AddComponent<AudioSource>();
            s.source.clip = s.clip;

            s.source.volume = s.volume;
            s.source.pitch = s.pitch;
            s.source.loop = s.loop;
        }
    }

    public void Play(string name)
    {
        Sound s = Array.Find(sounds, sound => sound.name == name);
        s.source.Play();
    }

    void Start()
    {
        Play("BackgroundMusic1");
    }

    //function to adjust volume through slider
    public void SetMusicVolume(float volume)
    {
        foreach (Sound s in sounds)
        {
            if (s.name == "BackgroundMusic1")
            {
                s.source.volume = volume;
            }
        }
    }

    public void SetSFXVolume(float volume)
    {
        foreach (Sound s in sounds)
        {
            if (s.name != "BackgroundMusic1")
            {
                s.source.volume = volume;
            }
        }
    }

    //function to link slider 
    public void SetMusicVolume()
    {
        SetMusicVolume(musicSlider.value);
    }

    public void SetSFXVolume()
    {
        SetSFXVolume(sfxSlider.value);
    }

}
