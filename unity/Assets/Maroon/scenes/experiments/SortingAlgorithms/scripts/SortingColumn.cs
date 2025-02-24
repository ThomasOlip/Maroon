﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SortingColumn : MonoBehaviour
{
    [SerializeField] private Texture2D sortingTexture;
    
    private SpriteRenderer _spriteRenderer;
    private int _numberOfColumns;
    private int _value;

    private bool _hidden;
    public bool Hidden
    {
        get => _hidden;
        set
        {
            _hidden = value;
            if(value)
                MarkNotSubset();
            else
                MarkSubset();
        }
    }

    private bool _marked;
    public bool Marked
    {
        get => _marked;
        set
        {
            _marked = value;
            if(value)
                Highlight();
            else
                UnHighlight();
        }
    }

    [SerializeField] private Color defaultColor;
    [SerializeField] private Color hiddenColor;
    [SerializeField] private Color highlightedColor;

    public int Value
    {
        get => _value;
        set
        {
            _value = value; 
            RerenderSprite();
        }
    }
    
    public void Init(int colNum, int position, int value)
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _spriteRenderer.color = defaultColor;
        _numberOfColumns = colNum;
        Value = value;
        var width = _spriteRenderer.bounds.size.x;
        transform.position += new Vector3((position - colNum / 2) * width, 0, 0);
    }

    public void Highlight()
    {
        _spriteRenderer.color = highlightedColor;
    }
    
    public void UnHighlight()
    {
        if(Hidden)
            _spriteRenderer.color = hiddenColor;
        else
            _spriteRenderer.color = defaultColor;
    }

    public void MarkSubset()
    {
        if(_spriteRenderer.color != highlightedColor)
            _spriteRenderer.color = defaultColor;
    }

    public void MarkNotSubset()
    {
        if(_spriteRenderer.color != highlightedColor)
            _spriteRenderer.color = hiddenColor;
    }

    private void RerenderSprite()
    {
        var height = sortingTexture.height;
        var width = sortingTexture.width;
        
        Rect textureStrip = new Rect(_value * width / _numberOfColumns, 0, width / _numberOfColumns, height);
        
        Sprite newSprite = Sprite.Create(sortingTexture, textureStrip, Vector2.zero);
        _spriteRenderer.sprite = newSprite;
    }
}
