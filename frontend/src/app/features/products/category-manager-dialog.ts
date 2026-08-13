import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { StoreApiService } from '../../core/services/store-api.service';
import { CategoryDto } from '../../core/models/api-types';
import { ConfirmDialog } from '../../shared/components';
import { BilingualPipe, TranslatePipe } from '../../shared/pipes';

@Component({
  selector: 'wo-category-manager-dialog',
  imports: [FormsModule, MatDialogModule, BilingualPipe, TranslatePipe],
  template: `
    <h2 mat-dialog-title>{{ 'products.categories.title' | t }}</h2>
    <mat-dialog-content>
      <div class="add-row">
        <input [placeholder]="'products.categories.name' | t" [(ngModel)]="newName" (keyup.enter)="add()" />
        <input [placeholder]="'products.categories.nameAr' | t" [(ngModel)]="newNameAr" dir="rtl" (keyup.enter)="add()" />
        <button class="wo-btn wo-btn--primary" type="button" (click)="add()" [disabled]="!newName.trim()">
          {{ 'common.add' | t }}
        </button>
      </div>

      @if (categories().length === 0) {
        <p class="muted empty">{{ 'products.categories.empty' | t }}</p>
      }

      <ul class="list">
        @for (category of categories(); track category.id) {
          <li>
            @if (editingId() === category.id) {
              <input class="edit-input" [(ngModel)]="editName" (keyup.enter)="saveEdit(category)" />
              <input class="edit-input" [(ngModel)]="editNameAr" dir="rtl" (keyup.enter)="saveEdit(category)" />
              <button class="icon-btn" type="button" (click)="saveEdit(category)">✔</button>
            } @else {
              <span class="name">{{ category.name | bilingual: category.nameAr }}</span>
              <span class="count muted">{{ 'products.categories.productsCount' | t: { count: category.productsCount } }}</span>
              <button class="icon-btn" type="button" (click)="startEdit(category)">✎</button>
              <button class="icon-btn" type="button" (click)="remove(category)">🗑</button>
            }
          </li>
        }
      </ul>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button class="wo-btn wo-btn--ghost" mat-dialog-close type="button">{{ 'common.close' | t }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .add-row { display: grid; grid-template-columns: 1fr 1fr auto; gap: 8px; margin-bottom: 16px;
      @media (max-width: 480px) { grid-template-columns: 1fr; } }
    input { border: 1.5px solid var(--wo-border); border-radius: 10px; padding: 9px 12px;
      font: 500 14px var(--wo-font); outline: none;
      &:focus { border-color: var(--wo-primary); } }
    .list { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px;
      li { display: flex; align-items: center; gap: 8px; padding: 8px 4px; border-bottom: 1px solid var(--wo-border); } }
    .name { font-weight: 600; flex: 1; }
    .count { font-size: 12px; }
    .edit-input { flex: 1; min-width: 0; }
    .icon-btn { border: none; background: none; cursor: pointer; font-size: 15px; opacity: .6; &:hover { opacity: 1; } }
    .empty { padding: 12px 0; }
  `,
})
export class CategoryManagerDialog {
  private readonly api = inject(StoreApiService);
  private readonly dialog = inject(MatDialog);

  readonly categories = signal<CategoryDto[]>([]);
  readonly editingId = signal<string | null>(null);

  newName = '';
  newNameAr = '';
  editName = '';
  editNameAr = '';

  constructor() {
    this.load();
  }

  private load(): void {
    this.api.getCategories().subscribe((categories) => this.categories.set(categories));
  }

  add(): void {
    const name = this.newName.trim();
    if (!name) return;
    this.api
      .createCategory({
        name,
        nameAr: this.newNameAr.trim() || null,
        sortOrder: this.categories().length,
        isActive: true,
      })
      .subscribe(() => {
        this.newName = '';
        this.newNameAr = '';
        this.load();
      });
  }

  startEdit(category: CategoryDto): void {
    this.editingId.set(category.id);
    this.editName = category.name;
    this.editNameAr = category.nameAr ?? '';
  }

  saveEdit(category: CategoryDto): void {
    const name = this.editName.trim();
    if (!name) return;
    this.api
      .updateCategory(category.id, {
        name,
        nameAr: this.editNameAr.trim() || null,
        sortOrder: category.sortOrder,
        isActive: category.isActive,
      })
      .subscribe(() => {
        this.editingId.set(null);
        this.load();
      });
  }

  remove(category: CategoryDto): void {
    this.dialog
      .open(ConfirmDialog, {
        data: {
          titleKey: 'products.categories.deleteTitle',
          messageKey: 'products.categories.deleteMessage',
          messageParams: { name: category.name },
          confirmKey: 'common.delete',
          danger: true,
        },
      })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) return;
        this.api.deleteCategory(category.id).subscribe(() => this.load());
      });
  }
}
