"""Remy's head texture paints the scalp a dark grey-brown; through the gaps of his lighter hair cards it reads as
a dark helmet. This writes Remy_Body_DiffuseH.png: the scalp recoloured to the hair's own colour (luminance kept,
roots a little darker), with a soft mask so the hairline stays soft. CarScene uses it on Remy's body material.

  python tools/remy_scalp.py
"""
import os

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
D = os.path.join(ROOT, "WeMadeIt_Unity", "Assets", "Resources", "Mixamo", "Textures", "remy")

body = np.asarray(Image.open(os.path.join(D, "Remy_Body_Diffuse.png")).convert("RGB")).astype(np.float32) / 255
hair = np.asarray(Image.open(os.path.join(D, "Remy_Hair_Diffuse.png")).convert("RGB")).astype(np.float32) / 255
hop = np.asarray(Image.open(os.path.join(D, "Remy_Hair_Opacity.png")).convert("L")).astype(np.float32) / 255
H, W, _ = body.shape

hair_mean = (hair * hop[..., None]).reshape(-1, 3).sum(0) / max(1.0, hop.sum())
# the scalp colour: sampled in the head's top corner, where only scalp is painted
scalp_ref = body[int(0.13 * H):int(0.17 * H), int(0.02 * W):int(0.06 * W)].reshape(-1, 3).mean(0)
print("hair mean", np.round(hair_mean * 255), "scalp ref", np.round(scalp_ref * 255))

# mask: close to the scalp colour, inside the head's block of the atlas (top-left, above the chin)
dist = np.linalg.norm(body - scalp_ref, axis=2)
mask = np.clip(1.0 - (dist - 0.1) / 0.15, 0, 1)        # wide: the crown is painted a little lighter than the sides
region = np.zeros((H, W), np.float32)
region[: int(0.40 * H), : int(0.50 * W)] = 1
mask *= region
mask = np.asarray(Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(6))).astype(np.float32) / 255

lum = body.mean(2, keepdims=True)
# strands: the hair cards' own streaks (their densest card, the left one), stretched over the scalp block
card = hair[:, : int(0.25 * hair.shape[1])].mean(2)
strand = np.asarray(Image.fromarray((card * 255).astype(np.uint8)).resize((W, H))).astype(np.float32) / 255
strand = (strand - strand.mean()) / max(1e-3, strand.std())
recol = hair_mean * 0.82 * (lum / max(1e-3, scalp_ref.mean())) * (1 + 0.18 * strand[..., None])
recol = np.clip(recol, 0, 1)                                         # hair colour, the scalp's shading, streaks
out = body * (1 - mask[..., None]) + recol * mask[..., None]
Image.fromarray((out * 255).astype(np.uint8)).save(os.path.join(D, "Remy_Body_DiffuseH.png"))
print("WROTE", os.path.join(D, "Remy_Body_DiffuseH.png"), "masked", round(float(mask.mean()) * 100, 1), "%")
